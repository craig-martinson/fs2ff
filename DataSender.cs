using fs2ff.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics; // Added

namespace fs2ff
{
    /// <summary>
    /// Transmits the data over ethernet to the EFB
    /// </summary>
    public class DataSender : IDisposable
    {
        private const int FlightSimPort = 49002;
        private const int Gdl90Port = 4000;
        private const string SimId = "MSFS";

        private List<IPEndPoint> _endPoints = new List<IPEndPoint>();
        private Socket? _socket;

        // Debug counters
        private long _attitudePackets;
        private long _positionPackets;
        private long _trafficPackets;
        private long _heartbeatPackets;
        private long _deviceStatusPackets;
        private long _ownerPackets;
        private long _rawStringPackets;
        private long _rawBytePackets;

        /// <summary>
        /// Binds to a socket for transmission
        /// </summary>
        /// <param name="ip"></param>
        public void Connect(IDictionary<string, IPAddress> ips)
        {
            Disconnect();
            int port = ViewModelLocator.Main.DataGdl90Enabled ? Gdl90Port : FlightSimPort;

            foreach (var ip in ips)
            {
                var endPoint = new IPEndPoint(ip.Value, port);
                _endPoints.Add(endPoint);
            }

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            Debug.WriteLine($"[DataSender] Socket bound for {(_endPoints.Count == 0 ? "NO" : _endPoints.Count.ToString())} endpoints on port {port}. GDL90={ViewModelLocator.Main.DataGdl90Enabled}");
        }

        public void Disconnect()
        {
            if (_socket != null)
            {
                Debug.WriteLine("[DataSender] Disconnecting socket.");
            }
            _socket?.Dispose();
            _socket = null;
        }

        public void Dispose() => _socket?.Dispose();

        /// <summary>
        /// Converts and sends an Attitude update packet
        /// </summary>
        public async Task Send(Attitude a)
        {
            Debug.WriteLine($"[DataSender] Sending Attitude packet. GDL90={ViewModelLocator.Main.DataGdl90Enabled}, Stratux={ViewModelLocator.Main.DataStratuxEnabled}");
            
            if (ViewModelLocator.Main.DataGdl90Enabled)
            {
                if (ViewModelLocator.Main.DataStratuxEnabled)
                {
                    Debug.WriteLine("[DataSender] → Using Gdl90Ahrs (full AHRS format)");
                    var ahrs = new Gdl90Ahrs(a);
                    var data = ahrs.ToGdl90Message();
                    Debug.WriteLine($"[DataSender] → GDL90 message size after framing: {data.Length} bytes");
                    await Send(data).ConfigureAwait(false);
                }
                else
                {
                    Debug.WriteLine("[DataSender] → Using Gdl90FfmAhrs (ForeFlight compact format)");
                    var ffAhrs = new Gdl90FfmAhrs(a);
                    var data = ffAhrs.ToGdl90Message();
                    Debug.WriteLine($"[DataSender] → GDL90 message size after framing: {data.Length} bytes");
                    await Send(data).ConfigureAwait(false);
                }
            }
            else
            {
                // X-Plane XATT format: heading, pitch, roll values
                // Note: MSFS provides positive pitch=nose up, but X-Plane expects negative for nose up
                var slipDeg = a.SkidSlip * -0.005;
                var data = string.Format(CultureInfo.InvariantCulture,
                    $"XATT{SimId},{a.TrueHeading:0.##},{-a.Pitch:0.##},{-a.Bank:0.##},,,{a.TurnRate:0.##},,,,{slipDeg:0.###},,");

                Debug.WriteLine($"[DataSender] → Using XATT format: {data}");
                await Send(data).ConfigureAwait(false);
            }
            Debug.WriteLine($"[DataSender] Attitude packet sent. Total attitude packets: {++_attitudePackets}");
        }

        /// <summary>
        /// Converts and sends a Position packet
        /// </summary>
        public async Task Send(Position p)
        {
            if (!ViewModelLocator.Main.DataGdl90Enabled)
            {
                var data = string.Format(CultureInfo.InvariantCulture,
                "XGPS{0},{1:0.#####},{2:0.#####},{3:0.##},{4:0.###},{5:0.##}",
                SimId, p.Pd.Longitude, p.Pd.Latitude, p.Pd.AltitudeMeters, p.Pd.GroundTrack, p.Pd.GroundSpeedMps);

                await Send(data).ConfigureAwait(false);
            }
            else
            {
                Gdl90GeoAltitude geoAlt = new Gdl90GeoAltitude(p);
                var data = geoAlt.ToGdl90Message();
                await Send(data).ConfigureAwait(false);
            }
            Debug.WriteLine($"[DataSender] Position packet sent. Total position packets: {++_positionPackets}");
        }

        /// <summary>
        /// Converts and sends a traffic packet.
        /// </summary>
        public async Task Send(Traffic t)
        {
            if (!t.IsValid())
            {
                Debug.WriteLine("[DataSender] Ignored invalid traffic packet.");
                return;
            }

            if (ViewModelLocator.Main.DataGdl90Enabled)
            {
                var traffic = new Gdl90Traffic(t);
                var data = traffic.ToGdl90Message();
                await Send(data).ConfigureAwait(false);
            }
            else
            {
                var data = string.Format(CultureInfo.InvariantCulture,
                    "XTRAFFIC{0},{1},{2:0.#####},{3:0.#####},{4:0.#},{5:0.#},{6},{7:0.###},{8:0.#},{9}",
                    SimId, t.Iaco, t.Td.Latitude, t.Td.Longitude, t.Td.Altitude, t.Td.VerticalSpeed, t.Td.OnGround ? 0 : 1,
                    t.Td.TrueHeading, t.Td.GroundVelocity, TryGetFlightNumber(t) ?? t.Td.TailNumber);

                await Send(data).ConfigureAwait(false);
            }
            if (t.IsOwner)
            {
                Debug.WriteLine($"[DataSender] Owner traffic packet sent. Total owner packets: {++_ownerPackets}");
            }
            else
            {
                Debug.WriteLine($"[DataSender] Traffic packet sent (id={t.ObjId}). Total traffic packets: {++_trafficPackets}");
            }
        }

        /// <summary>
        /// Encodes and sends a string
        /// </summary>
        private async Task Send(string data)
        {
            if (_socket != null)
            {
                var byteData = new ArraySegment<byte>(Encoding.ASCII.GetBytes(data));
                foreach (var endPoint in _endPoints)
                {
                    await _socket
                        .SendToAsync(byteData, SocketFlags.None, endPoint)
                        .ConfigureAwait(false);
                }
                Debug.WriteLine($"[DataSender] Raw string packet ({data.Length} chars) sent to {_endPoints.Count} endpoints. Total raw string packets: {++_rawStringPackets}");
            }
            else
            {
                Debug.WriteLine("[DataSender] Attempted to send string but socket is null.");
            }
        }

        /// <summary>
        /// Sends the given byte array
        /// </summary>
        public async Task Send(byte[] data)
        {
            if (_socket != null)
            {
                foreach (var endPoint in _endPoints)
                {
                    await _socket
                        .SendToAsync(data, SocketFlags.None, endPoint)
                        .ConfigureAwait(false);
                }
                Debug.WriteLine($"[DataSender] Raw byte packet ({data.Length} bytes) sent to {_endPoints.Count} endpoints. Total raw byte packets: {++_rawBytePackets}");
            }
            else
            {
                Debug.WriteLine("[DataSender] Attempted to send bytes but socket is null.");
            }
        }

        private static string? TryGetFlightNumber(Traffic t) =>
            !string.IsNullOrEmpty(t.Td.Airline) && !string.IsNullOrEmpty(t.Td.FlightNumber)
                ? $"{t.Td.Airline} {t.Td.FlightNumber}"
                : null;

        /// <summary>
        /// Used by timers to mark heartbeat/device packets
        /// </summary>
        public void MarkHeartbeatSent()
        {
            Debug.WriteLine($"[DataSender] Heartbeat sent. Total heartbeats: {++_heartbeatPackets}");
        }

        public void MarkDeviceStatusSent()
        {
            Debug.WriteLine($"[DataSender] Device status sent. Total device status packets: {++_deviceStatusPackets}");
        }
    }
}

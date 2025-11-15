using System;
using System.Diagnostics;

namespace fs2ff.Models
{
    public class Gdl90FfmAhrs : Gdl90Base
    {
        /// <summary>
        /// ForeFlight's implementation of GDL90 AHRS 10hz 12 bytes
        /// Compact format used by ForeFlight and Garmin Pilot
        /// All data sourced directly from SimConnect Attitude struct
        /// Unfortunately Garmin Pilot is also using this limited data instead of Gdl90Ahrs.
        /// GP is working on using a more generic GDL90 implementation but until then
        /// </summary>
        /// <param name="att">Attitude data from SimConnect</param>
        public Gdl90FfmAhrs(Attitude att) : base(12)
        {
            Msg[0] = 0x65; // Message type "ForeFlight".
            Msg[1] = 0x01; // AHRS message identifier.

            // pitch, roll, heading have an LSB = 0.1
            var pitch = Convert.ToInt16(att.Pitch * -10);  // MSFS positive = nose up, negate for GDL90
            var roll = Convert.ToInt16(att.Bank * -10);    // MSFS positive = right bank, negate for GDL90
            var hdg = Convert.ToInt16(att.TrueHeading * 10); // True heading from SimConnect

            var ias = Convert.ToInt16(att.AirspeedIndicated);  // Indicated airspeed in knots
            var tas = Convert.ToInt16(att.AirspeedTrue);       // True airspeed in knots

            // Debug output showing source values and encoded values
            Debug.WriteLine($"[Gdl90FfmAhrs] Encoding ForeFlight AHRS data:");
            Debug.WriteLine($"  Pitch: {att.Pitch:0.##}° → encoded: {pitch} (LSB=0.1°)");
            Debug.WriteLine($"  Roll: {att.Bank:0.##}° → encoded: {roll} (LSB=0.1°)");
            Debug.WriteLine($"  Heading: {att.TrueHeading:0.##}° → encoded: {hdg} (LSB=0.1°)");
            Debug.WriteLine($"  IAS: {att.AirspeedIndicated:0.##} kts → encoded: {ias} kts");
            Debug.WriteLine($"  TAS: {att.AirspeedTrue:0.##} kts → encoded: {tas} kts");

            // Roll (bytes 2-3)
            Msg[2] = (byte)((roll >> 8) & 0xFF);
            Msg[3] = (byte)(roll & 0xFF);

            // Pitch (bytes 4-5)
            Msg[4] = (byte)((pitch >> 8) & 0xFF);
            Msg[5] = (byte)(pitch & 0xFF);

            // Heading (bytes 6-7) - True heading from SimConnect
            Msg[6] = (byte)((hdg >> 8) & 0xFF);
            Msg[7] = (byte)(hdg & 0xFF);

            // Indicated Airspeed (bytes 8-9)
            Msg[8] = (byte)((ias >> 8) & 0xFF);
            Msg[9] = (byte)(ias & 0xFF);

            // True Airspeed (bytes 10-11) - 12-bit encoding
            Msg[10] = (byte)((tas & 0xFF0) >> 4);
            Msg[11] = (byte)((tas & 0x00F) << 4);

            // Output the complete message in hex format
            var hexMsg = BitConverter.ToString(Msg).Replace("-", " ");
            Debug.WriteLine($"[Gdl90FfmAhrs] Raw message bytes: {hexMsg}");
        }
    }
}

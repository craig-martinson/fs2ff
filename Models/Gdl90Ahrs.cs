using System;
using System.Diagnostics;

namespace fs2ff.Models
{
    public class Gdl90Ahrs : Gdl90Base
    {
        /// <summary>
        /// Standard GDL90 AHRS implementation 24 bytes long
        /// Per GDL90 spec, this message provides attitude, heading, and airspeed data
        /// All data sourced directly from SimConnect Attitude struct
        /// </summary>
        /// <param name="att">Attitude data from SimConnect</param>
        public Gdl90Ahrs(Attitude att) : base(24)
        {
            Msg[0] = 0x4c;  // Message ID byte 1
            Msg[1] = 0x45;  // Message ID byte 2
            Msg[2] = 0x01;  // Version
            Msg[3] = 0x01;  // Sub-version

            // The following values have been adjusted for GP SV to match the MSFS 172 Skyhawk G1000
            // All of the following have an LSB = 0.1 degrees or 0.1 unit
            var pitch = Convert.ToInt16(att.Pitch * -10); // MSFS reverses the values (positive = nose up)
            var roll = Convert.ToInt16(att.Bank * -10); // MSFS reverses the values (positive = right bank)
            var hdg = Convert.ToInt16(att.TrueHeading * 10);
            var slipSkid = Convert.ToInt16(att.SkidSlip * 2.8);  // * 10 was too high in SU 14, adjusted down
            var yaw = Convert.ToInt16(att.TurnRate * 10);
            var g = Convert.ToInt16((att.GForce * 10).AdjustToBounds(short.MinValue + 1, short.MaxValue - 1));

            var palt = Convert.ToInt32(att.PressureAlt.AdjustToBounds(short.MinValue + 1, short.MaxValue - 1));
            var ias = Convert.ToInt16(att.AirspeedIndicated.AdjustToBounds(short.MinValue + 1, short.MaxValue - 1));
            var vs = Convert.ToInt16(att.VertSpeed.AdjustToBounds(short.MinValue + 1, short.MaxValue - 1));

            // Debug output showing source values and encoded values
            Debug.WriteLine($"[Gdl90Ahrs] Encoding AHRS data:");
            Debug.WriteLine($"  Pitch: {att.Pitch:0.##}° → encoded: {pitch} (LSB=0.1°)");
            Debug.WriteLine($"  Roll: {att.Bank:0.##}° → encoded: {roll} (LSB=0.1°)");
            Debug.WriteLine($"  Heading: {att.TrueHeading:0.##}° → encoded: {hdg} (LSB=0.1°)");
            Debug.WriteLine($"  Slip/Skid: {att.SkidSlip:0.##}° → encoded: {slipSkid}");
            Debug.WriteLine($"  Yaw Rate: {att.TurnRate:0.##}°/s → encoded: {yaw} (LSB=0.1°/s)");
            Debug.WriteLine($"  G-Force: {att.GForce:0.##} → encoded: {g} (LSB=0.1)");
            Debug.WriteLine($"  IAS: {att.AirspeedIndicated:0.##} kts → encoded: {ias} kts");
            Debug.WriteLine($"  Pressure Alt: {att.PressureAlt:0.##} ft → encoded: {palt} ft");
            Debug.WriteLine($"  Vert Speed: {att.VertSpeed:0.##} fpm → encoded: {vs} fpm");

            // Roll (bytes 4-5).
            Msg[4] = (byte)((roll >> 8) & 0xFF);
            Msg[5] = (byte)(roll & 0xFF);

            // Pitch (bytes 6-7).
            Msg[6] = (byte)((pitch >> 8) & 0xFF);
            Msg[7] = (byte)(pitch & 0xFF);

            // Heading (bytes 8-9) - True heading from SimConnect
            Msg[8] = (byte)((hdg >> 8) & 0xFF);
            Msg[9] = (byte)(hdg & 0xFF);

            // Slip/skid (bytes 10-11).
            Msg[10] = (byte)((slipSkid >> 8) & 0xFF);
            Msg[11] = (byte)(slipSkid & 0xFF);

            // Yaw rate (bytes 12-13).
            Msg[12] = (byte)((yaw >> 8) & 0xFF);
            Msg[13] = (byte)(yaw & 0xFF);

            // G-force (bytes 14-15).
            Msg[14] = (byte)((g >> 8) & 0xFF);
            Msg[15] = (byte)(g & 0xFF);

            // Indicated Airspeed (bytes 16-17) - Knots
            Msg[16] = (byte)((ias >> 8) & 0xFF);
            Msg[17] = (byte)(ias & 0xFF);

            // Pressure Altitude (bytes 18-19) - Feet
            Msg[18] = (byte)((palt >> 8) & 0xFF);
            Msg[19] = (byte)(palt & 0xFF);

            // Vertical Speed (bytes 20-21) - Feet per minute
            Msg[20] = (byte)((vs >> 8) & 0xFF);
            Msg[21] = (byte)(vs & 0xFF);

            // Reserved (bytes 22-23)
            Msg[22] = 0x7F;
            Msg[23] = 0xFF;

            // Output the complete message in hex format
            var hexMsg = BitConverter.ToString(Msg).Replace("-", " ");
            Debug.WriteLine($"[Gdl90Ahrs] Raw message bytes: {hexMsg}");
        }
    }
}

using System;

namespace SpeedifyG19Applet
{
    public class MeasureModel
    {
        public MeasureModel(long tick, long value)
        {
            this.Tick = tick;
            this.Value = value;
        }

        public long Tick { get; set; }
        public long Value { get; set; }
    }
}
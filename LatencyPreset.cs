namespace AudioPitchShifter
{
    public class LatencyPreset
    {
        public string Name { get; set; }
        public int InputBufferMs { get; set; }
        public int OutputBufferMs { get; set; }
        public string Description { get; set; }

        public LatencyPreset(string name, int inputBufferMs, int outputBufferMs, string description)
        {
            Name = name;
            InputBufferMs = inputBufferMs;
            OutputBufferMs = outputBufferMs;
            Description = description;
        }

        public override string ToString()
        {
            return $"{Name} (~{InputBufferMs + OutputBufferMs}ms total)";
        }

        public static LatencyPreset[] GetPresets()
        {
            return new[]
            {
                // new LatencyPreset("Low", 20, 50, "Fast response - recommended"),
                new LatencyPreset("Low", 10, 50, "Fast response - recommended"),
                new LatencyPreset("Medium", 30, 70, "Balanced - good for most systems"),
                new LatencyPreset("High", 40, 100, "Most stable - highest compatibility")
            };
        }
    }
}

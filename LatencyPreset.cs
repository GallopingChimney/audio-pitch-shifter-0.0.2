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
    }
}

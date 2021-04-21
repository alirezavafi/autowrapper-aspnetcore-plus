namespace AutoWrapper
{
    public class AutoWrapperExcludePath
    {
        public AutoWrapperExcludePath(string path, ExcludeMode excludeMode = ExcludeMode.Strict)
        {
            Path = path;
            ExcludeMode = excludeMode;
        }

        public string Path { get; set; }

        public ExcludeMode ExcludeMode { get; set; }
    }
}
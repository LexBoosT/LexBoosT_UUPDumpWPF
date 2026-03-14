namespace UUPDumpWPF.Models
{
    public class Build
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string BuildNumber { get; set; } = string.Empty;
        public bool IsRetail { get; set; }
        public string Architecture { get; set; } = string.Empty;
    }

    public class Language
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class Edition
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsVirtual { get; set; } = false;
        public string? BaseEditionCode { get; set; } = null;
    }

    public class WindowsVersion
    {
        public string Build { get; set; } = string.Empty;
        public string FullVersion { get; set; } = string.Empty;
    }
}

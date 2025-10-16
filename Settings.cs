namespace BureauAdaptor
{
    public class Settings
    {
        public AdaptorSettings? AdaptorSettings { get; set; }
        public BureauSettings? BureauSettings { get; set; }     
    }

    public class AdaptorSettings
    {
        public Int32 Port { get; set; }
        public Int32 MaxConnections { get; set; }
    }

    public class BureauSettings
    {
        public string? URL { get; set; }
        public string? SystemId { get; set; }
        public string? Password { get; set; }
        public string? ProcessingEnvironment { get; set; }
        public Int32 Timeout { get; set; }
        public string? CertificatePath { get; set; }
        public string? RequestMethodType { get; set; }
        public string? TokenURL { get; set; }
        public string? ClientID { get; set; }
        public string? ClientSecretCode { get; set; }
        public string? UserName { get; set; }
        public string? FileSystem { get; set; }
        public string? ErrorEmailMessage { get; set; }
        public string? Scope { get; set; }
    }
}
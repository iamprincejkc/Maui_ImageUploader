using System.Threading.Tasks;

namespace MauiImageUploader.Services
{
    public interface IFtpService
    {
        /// <summary>
        /// Test FTP connection with provided configuration
        /// </summary>
        FtpTestResult TestConnection(FtpConfiguration config);

        /// <summary>
        /// Process images and upload to FTP server
        /// </summary>
        BulkProcessingResult ProcessAndUpload(string inputFolderPath, FtpConfiguration config, IProgress<BulkProcessingProgress> progress);
    }

    public class FtpTestResult
    {
        public bool IsSuccessful { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorDetails { get; set; } = string.Empty;
    }

    public class FtpConfiguration
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 21;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RemotePath { get; set; } = "/";
    }
}
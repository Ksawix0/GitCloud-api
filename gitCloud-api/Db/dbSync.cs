using System.Text;
using static gitCloud_api.Lake;

namespace gitCloud_api;

public static partial class Db
{
    public class DbSync : BackgroundService
    {
        private const ushort UploadDelayInMinutes = 60;

        public static async Task UploadUsersUpstream()
        {
            GetContentClass getContent =  await GetLakeRequest(GitCloudDbFilePaths.UserFile);
            PutContentClass putContentClass = await PutLakeRequest(GitCloudDbFilePaths.UserFile,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.Users))), getContent.Sha);
            if (putContentClass.ErrorCode != null)
            {
                Logger.LogError("db sync error: {message}",  putContentClass.ErrorMessage);
            }
        }
        
        public static async Task UploadTokensUpstream()
        {
            GitCloudDb.RefreshTokens.RemoveAll(token => token.ExpiresAt < DateTime.Now);
            
            GetContentClass getContent =  await GetLakeRequest(GitCloudDbFilePaths.RefreshTokenFile);
            PutContentClass putContentClass = await PutLakeRequest(GitCloudDbFilePaths.RefreshTokenFile,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.RefreshTokens))), getContent.Sha);
            if (putContentClass.ErrorCode != null)
            {
                Logger.LogError("db sync error: {message}",  putContentClass.ErrorMessage);
            }
        }
        
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Upload db upstream background service started");
            await Task.Delay(TimeSpan.FromMinutes(UploadDelayInMinutes), cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await UploadUsersUpstream();
                await UploadTokensUpstream();
                await Task.Delay(TimeSpan.FromMinutes(UploadDelayInMinutes), cancellationToken);
            }
        }
    }
}

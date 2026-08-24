using System.Text;
using static gitCloud_api.Lake;

namespace gitCloud_api;

public static partial class Db
{
    public class DbSync : BackgroundService
    {
        private const ushort UploadDelayInMinutes = 60;
        public static async Task UploadUsersUpstream(CancellationToken cancellationToken)
        {
            GraphQlGetClass restGet =  await GetLakeInfo(GitCloudDbFilePaths.UserFile, cancellationToken);
            MemoryStream userStream = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.Users)))));
            RestPutClass restPutClass = await PutLakeRequest(GitCloudDbFilePaths.UserFile, userStream, userStream.Length, cancellationToken, restGet.Data.Repository.Object.Oid);
            if (restPutClass.StatusCode != 200)
            {
                Logger.LogError("db sync error, status code: {code}",  restPutClass.StatusCode);
            }
        }
        
        public static async Task UploadTokensUpstream(CancellationToken cancellationToken)
        {
            GitCloudDb.RefreshTokens.RemoveAll(token => token.ExpiresAt < DateTime.Now);
            
            GraphQlGetClass restGet =  await GetLakeInfo(GitCloudDbFilePaths.RefreshTokenFile, cancellationToken);
            MemoryStream tokenStream = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(GitCloudDbSerializer.FileSerializer(GitCloudDb.RefreshTokens)))));
            RestPutClass restPutClass = await PutLakeRequest(GitCloudDbFilePaths.RefreshTokenFile, tokenStream, tokenStream.Length, cancellationToken, restGet.Data.Repository.Object.Oid);
            
            if (restPutClass.StatusCode != 200)
            {
                Logger.LogError("db sync error, status code: {code}",  restPutClass.StatusCode);
            }
        }
        
        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Upload db upstream background service started");
            await Task.Delay(TimeSpan.FromMinutes(UploadDelayInMinutes), cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await UploadUsersUpstream(cancellationToken);
                await UploadTokensUpstream(cancellationToken);
                await Task.Delay(TimeSpan.FromMinutes(UploadDelayInMinutes), cancellationToken);
            }
        }
    }
}

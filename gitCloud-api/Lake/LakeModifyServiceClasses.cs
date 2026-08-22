namespace gitCloud_api;
using static Lake;

public static class LakeModifyServiceClasses
{
    public abstract class LakeModifyRequest
    {
        public required string Path;
        public CancellationToken CancellationToken = CancellationToken.None;
    }

    public class LakePutRequest : LakeModifyRequest
    {
        public required Task<string> BodyContentTask;
    }
    
    public class LakeDelRequest : LakeModifyRequest { }
    
    public class FullLakeModifyRequest(TaskCompletionSource<IResponseRest> tcs, LakeModifyRequest lakeModifyRequest)
    {
        public readonly TaskCompletionSource<IResponseRest> TaskCompletionSource = tcs;
        public readonly LakeModifyRequest ModifyRequest = lakeModifyRequest;
    } 
}
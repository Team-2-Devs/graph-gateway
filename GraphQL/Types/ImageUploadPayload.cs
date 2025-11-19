namespace GraphGateway.GraphQL.Types;

public record ImageUploadPayload(string UploadId, string Key, string PutUrl, DateTimeOffset ExpiresAt);
namespace GraphGateway.GraphQL.Types;

public record StartUploadPayload(string CorrelationId, ImageUploadPayload ImageUploadPayload);
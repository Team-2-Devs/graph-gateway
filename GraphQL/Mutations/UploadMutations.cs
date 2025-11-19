using HotChocolate.Authorization;
using GraphGateway.GraphQL.Types;

namespace GraphGateway.GraphQL.Mutations;

[ExtendObjectType("Mutation")]
public class UploadMutations
{
    [Authorize(Policy = "RequireApiScope")]
    public async Task<StartUploadPayload> StartUploadAsync(string filename, string contentType,
        [Service] IHttpClientFactory httpClientFactory, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient("ingestion");
        var request = new { filename, contentType };

        var response = await http.PostAsJsonAsync("/v1/uploads/start", request, ct);

        response.EnsureSuccessStatusCode();

        var imagePayload = await response.Content.ReadFromJsonAsync<ImageUploadPayload>(cancellationToken: ct)
               ?? throw new Exception("Empty response");

        string correlationId = Guid.NewGuid().ToString("n");
               
        return new StartUploadPayload(correlationId, imagePayload);
    }

    [Authorize(Policy = "RequireApiScope")]
    public async Task<ConfirmUploadPayload> ConfirmUploadAsync(string uploadId, int bytes, string checksum,
        [Service] IHttpClientFactory httpClientFactory, CancellationToken ct)
    {
        var http = httpClientFactory.CreateClient("ingestion");

        var request = new { uploadId, bytes, checksum };

        var response = await http.PostAsJsonAsync("/v1/uploads/confirm", request, ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ConfirmUploadPayload>(cancellationToken: ct)
               ?? throw new Exception("Empty response");
    }
}
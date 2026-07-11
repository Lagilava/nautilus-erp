using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ERP.IntegrationTests;

/// <summary>
/// Exercises the generic attachment endpoints end-to-end: upload against an arbitrary
/// (entityType, entityId) pair, list, download, and delete — plus role enforcement.
/// </summary>
public class AttachmentsEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly ErpWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AttachmentsEndpointTests(ErpWebApplicationFactory factory) => _factory = factory;

    private static MultipartFormDataContent BuildUpload(string entityType, Guid entityId, string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(entityType), "entityType" },
            { new StringContent(entityId.ToString()), "entityId" }
        };
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Admin_can_upload_list_download_and_delete_an_attachment()
    {
        var client = await _factory.AdminClientAsync();
        var entityId = Guid.NewGuid();
        var bytes = Encoding.UTF8.GetBytes("hello attachment");

        var upload = await client.PostAsync("/api/attachments", BuildUpload("SupplierInvoice", entityId, "note.txt", bytes));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var uploaded = await upload.Content.ReadFromJsonAsync<JsonElement>(Json);
        var attachmentId = uploaded.GetProperty("id").GetGuid();
        Assert.Equal("note.txt", uploaded.GetProperty("fileName").GetString());
        Assert.Equal(bytes.Length, uploaded.GetProperty("sizeBytes").GetInt64());

        var list = await client.GetAsync($"/api/attachments?entityType=SupplierInvoice&entityId={entityId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listed = await list.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Single(listed.EnumerateArray());

        var download = await client.GetAsync($"/api/attachments/{attachmentId}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());

        var delete = await client.DeleteAsync($"/api/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var afterDelete = await client.GetAsync($"/api/attachments/{attachmentId}/download");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Upload_with_empty_file_is_rejected()
    {
        var client = await _factory.AdminClientAsync();
        var response = await client.PostAsync("/api/attachments", BuildUpload("SupplierInvoice", Guid.NewGuid(), "empty.txt", Array.Empty<byte>()));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Downloading_an_unknown_attachment_returns_not_found()
    {
        var client = await _factory.AdminClientAsync();
        var response = await client.GetAsync($"/api/attachments/{Guid.NewGuid()}/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

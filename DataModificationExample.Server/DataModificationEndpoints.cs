using System.Text.Json;
using DataModificationExample.Server.DataManagement;

namespace DataModificationExample.Server;

public record ErrorResponse(string Error);
public record DataModificationListResponse(List<DataModificationInfo> DataModifications);
public record DataModificationRunRequest(string DataModificationName, string? ConfirmationName = null);

public static class DataModificationEndpoints
{
    public static IEndpointRouteBuilder MapDataModificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/dataModifications");

        group.MapGet("/", (IDataModificationRegistry registry) =>
        {
            var dataModifications = registry.GetAllDataModifications().ToList();
            return TypedResults.Ok(new DataModificationListResponse(dataModifications));
        });

        group.MapPost("/dryRun", async (
            DataModificationRunRequest request,
            IDataModificationRegistry registry,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var dataModification = registry.GetDataModification(request.DataModificationName);
            if (dataModification == null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                await httpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse($"Data modification '{request.DataModificationName}' not found"),
                    cancellationToken);
                return;
            }

            SetupSseResponse(httpContext);
            await StreamEvents(httpContext.Response, dataModification.RunDryRun(cancellationToken));
        });

        group.MapPost("/run", async (
            DataModificationRunRequest request,
            IDataModificationRegistry registry,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(request.ConfirmationName) || request.ConfirmationName != request.DataModificationName)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await httpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse("Data modification name confirmation does not match"),
                    cancellationToken);
                return;
            }

            var dataModification = registry.GetDataModification(request.DataModificationName);
            if (dataModification == null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                await httpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse($"Data modification '{request.DataModificationName}' not found"),
                    cancellationToken);
                return;
            }

            SetupSseResponse(httpContext);
            await StreamEvents(httpContext.Response, dataModification.RunModification(cancellationToken));
        });

        return endpoints;
    }

    private static void SetupSseResponse(HttpContext httpContext)
    {
        httpContext.Response.Headers["Content-Type"] = "text/event-stream";
        httpContext.Response.Headers["Cache-Control"] = "no-cache";
        httpContext.Response.Headers["Connection"] = "keep-alive";
    }

    private static async Task StreamEvents(HttpResponse response, IAsyncEnumerable<DataModificationLogEvent> events)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await foreach (var logEvent in events)
        {
            // Serialize as base type to include the polymorphic type discriminator
            var json = JsonSerializer.Serialize<DataModificationLogEvent>(logEvent, options);
            await response.WriteAsync($"data: {json}\n\n");
            await response.Body.FlushAsync();
        }
    }
}

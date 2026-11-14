using System.Text.Json;
using Api.Infrastructure;
using Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IntegrationTests;

public sealed class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string> Cases => new()
    {
        { new ValidationException([new ValidationFailure("Name", "Name is required.")]), 400, "Validation failed." },
        { new NotFoundException("Work order 'x' was not found."), 404, "Work order 'x' was not found." },
        { new BusinessRuleException("Work order state changed."), 409, "Work order state changed." },
        { new InvalidOperationException("boom"), 500, "An unexpected error occurred." }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task TryHandle_ShouldMapExceptionToProblemDetails(Exception exception, int status, string title)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.True(handled);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.StartsWith("application/problem+json", context.Response.ContentType);
        Assert.Equal(title, body.RootElement.GetProperty("title").GetString());
        if (status == 400)
        {
            Assert.Equal("Name is required.", body.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString());
        }
    }
}

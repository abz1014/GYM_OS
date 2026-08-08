using System.Net;
using System.Text.Json;
using FluentValidation;
using GymOS.Application.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace GymOS.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, errors) = exception switch
        {
            /*
             * Two shapes of ValidationException reach here and they need different titles.
             *
             * FluentValidation's own failures arrive with a populated `Errors` collection and a
             * generic Message, so "Validation failed" plus the per-field dictionary is right.
             *
             * A handler that throws `new ValidationException("This plan allows at most 7 freeze
             * day(s)")` puts that sentence in `Message` and leaves `Errors` EMPTY — and the old code
             * mapped it to `{"title":"Validation failed","errors":{}}`, dropping the only part the
             * user needed. Every hand-thrown business rule in the app was surfacing as an
             * unexplained 400, which the frontend then rendered as "Could not do X".
             */
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                validationEx.Errors.Any() ? "Validation failed" : validationEx.Message,
                validationEx.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            NotFoundException notFoundEx => (HttpStatusCode.NotFound, notFoundEx.Message, null),
            UnauthorizedAccessException unauthorizedEx => (HttpStatusCode.Unauthorized, unauthorizedEx.Message, null),
            ForbiddenAccessException forbiddenEx => (HttpStatusCode.Forbidden, forbiddenEx.Message, null),
            // 429, not 400: this one comes back on its own, so a client retrying later is correct.
            RateLimitExceededException rateLimitEx => (HttpStatusCode.TooManyRequests, rateLimitEx.Message, null),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", null)
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        }

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Extensions = { ["errors"] = errors }
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
}

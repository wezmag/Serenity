﻿using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Serenity.Services
{
    public static class EndpointExtensions
    {
        public static TResponse ConvertToResponse<TResponse>(this Exception exception, HttpContext httpContext)
            where TResponse : ServiceResponse, new()
        {
            return ConvertToResponse<TResponse>(exception,
                httpContext?.RequestServices?.GetService<IExceptionLogger>(),
                httpContext?.RequestServices?.GetService<ITextLocalizer>(),
                string.Equals(httpContext?.RequestServices.GetService<IWebHostEnvironment>()?
                    .EnvironmentName, "development", StringComparison.OrdinalIgnoreCase));
        }

        public static TResponse ConvertToResponse<TResponse>(this Exception exception, IExceptionLogger logger, 
            ITextLocalizer localizer, bool showDetails)
            where TResponse: ServiceResponse, new()
        {
            exception.Log(logger);

            var response = new TResponse();
            
            var error = new ServiceError
            {
                Message = (showDetails || 
                    (exception is IIsSensitiveMessage isSensitive && 
                     !isSensitive.IsSensitiveMessage)) ?
                        exception.Message : localizer?.TryGet("Services.GenericErrorMessage") ??
                            "An error occurred while processing your request."
            };

            if (exception is ValidationError ve)
            {
                error.Code = ve.ErrorCode;
                error.Arguments = ve.Arguments;
                if (showDetails)
                    error.Details = ve.ToString();
            }
            else
            {
                error.Code = "Exception";

                if (showDetails)
                    error.Details = exception?.ToString();
            }

            if (exception != null &&
                exception.Data != null &&
                exception.Data.Contains(nameof(error.ErrorId)))
                error.ErrorId = exception.Data[nameof(error.ErrorId)]?.ToString();

            response.Error = error;
            return response;
        }

        public static Result<TResponse> ExecuteMethod<TResponse>(this Controller controller, Func<TResponse> handler)
            where TResponse: ServiceResponse, new()
        {
            TResponse response;
            try
            {
                response = handler();
            }
            catch (Exception exception)
            {
                response = exception.ConvertToResponse<TResponse>(controller.HttpContext);
                controller.HttpContext.Response.Clear();
                controller.HttpContext.Response.StatusCode = exception is ValidationError ? 400 : 500;
            }

            return new Result<TResponse>(response);
        }

        public static Result<TResponse> UseConnection<TResponse>(this Controller controller, string connectionKey, Func<IDbConnection, TResponse> handler)
            where TResponse : ServiceResponse, new()
        {
            TResponse response;
            try
            {
                var factory = controller.HttpContext.RequestServices.GetRequiredService<ISqlConnections>();
                using var connection = factory.NewByKey(connectionKey);
                response = handler(connection);
            }
            catch (Exception exception)
            {
                response = exception.ConvertToResponse<TResponse>(controller.HttpContext);
                controller.HttpContext.Response.Clear();
                controller.HttpContext.Response.StatusCode = exception is ValidationError ? 400 : 500;
            }

            return new Result<TResponse>(response);
        }


        public static Result<TResponse> InTransaction<TResponse>(this Controller controller, string connectionKey, Func<IUnitOfWork, TResponse> handler)
            where TResponse : ServiceResponse, new()
        {
            TResponse response;
            try
            {
                var factory = controller.HttpContext.RequestServices.GetRequiredService<ISqlConnections>();

                using var connection = factory.NewByKey(connectionKey);
                using var uow = new UnitOfWork(connection);
                response = handler(uow);
                uow.Commit();
            }
            catch (Exception exception)
            {
                response = exception.ConvertToResponse<TResponse>(controller.HttpContext);
                controller.HttpContext.Response.Clear();
                controller.HttpContext.Response.StatusCode = exception is ValidationError ? 400 : 500;
            }

            return new Result<TResponse>(response);
        }
    }
}
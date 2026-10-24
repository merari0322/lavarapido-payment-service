using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PaymentService.Api.Configuration;
using Xunit;

namespace PaymentService.Api.UnitTests.Errors;

// BIZ-13 / E-18: el 400 automático de un modelo inválido trae el code que traducen la web y la app.
public class ValidationErrorCodeTests
{
    [Fact]
    public void AnInvalidBody_IsAValidationProblemWithTheValidationErrorCode()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers().AddValidationErrorCode();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;

        var context = new ActionContext(new DefaultHttpContext { RequestServices = provider }, new RouteData(),
            new ActionDescriptor());
        context.ModelState.AddModelError("$", "JSON deserialization was missing required properties, including: bookingId.");

        var response = Assert.IsAssignableFrom<ObjectResult>(options.InvalidModelStateResponseFactory(context));
        var problem = Assert.IsType<ValidationProblemDetails>(response.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("VALIDATION_ERROR", problem.Extensions["code"]);
        Assert.Contains("$", problem.Errors.Keys);
    }
}

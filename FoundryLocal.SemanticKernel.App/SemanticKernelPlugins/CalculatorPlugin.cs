using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

public sealed class CalculatorPlugin
{
    private readonly ILogger<CalculatorPlugin> _logger;

    public CalculatorPlugin(ILogger<CalculatorPlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction("add")]
    [Description("Add two numbers together")]
    public double Add(
        [Description("First number")] double a,
        [Description("Second number")] double b)
    {
        _logger.LogInformation("Adding {A} + {B}", a, b);

        return a + b;
    }

    [KernelFunction("divide")]
    [Description("Divide two numbers")]
    public double Divide(
        [Description("Dividend")] double a,
        [Description("Divisor")] double b)
    {
        _logger.LogInformation("Dividing {A} / {B}", a, b);

        if (b == 0)
        {
            _logger.LogWarning("Division by zero attempted");

            throw new DivideByZeroException("Cannot divide by zero");
        }

        return Math.Round(a / b, 2);
    }
}
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Data;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

[Description("A plugin that provides basic calculator functions.")]
public sealed class CalculatorPlugin
{
    private readonly ILogger<CalculatorPlugin> _logger;
    public CalculatorPlugin(ILogger<CalculatorPlugin>? logger = null)
    {
        _logger = logger ?? new LoggerFactory().CreateLogger<CalculatorPlugin>();
    }

    [KernelFunction("calculate")]
    [Description("Evaluates a simple mathematical expression and returns the result. Supports basic operations like addition, subtraction, multiplication, and division.")]
    public string Calculate(
        [Description("The mathematical expression to evaluate")]
        string expression)
    {
        try
        {
            var result = new DataTable().Compute(expression, null);
            _logger.LogInformation("Calculated expression '{Expression}' with result: {Result}", expression, result);
            return result.ToString() ?? "Error: Unable to compute result.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating expression '{Expression}'", expression);
            return $"Error: Invalid expression. {ex.Message}";
        }
    }
}
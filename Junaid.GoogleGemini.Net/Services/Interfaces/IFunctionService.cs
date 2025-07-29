using Junaid.GoogleGemini.Net.Models.Functions;

namespace Junaid.GoogleGemini.Net.Services.Interfaces
{
    /// <summary>
    /// Service for managing function calls from Gemini model
    /// </summary>
    public interface IFunctionService
    {
        /// <summary>
        /// Registers a function that can be called by the model
        /// </summary>
        /// <param name="definition">The function definition</param>
        /// <param name="handler">The function handler that will be called</param>
        void RegisterFunction(FunctionDefinition definition, Func<Dictionary<string, object>, Task<object>> handler);

        /// <summary>
        /// Gets all registered function definitions
        /// </summary>
        IReadOnlyList<FunctionDefinition> GetAvailableFunctions();

        /// <summary>
        /// Calls a function by name with the provided arguments
        /// </summary>
        /// <param name="functionCall">The function call details</param>
        /// <returns>The result of the function call</returns>
        Task<FunctionResult> CallFunctionAsync(FunctionCall functionCall);

        /// <summary>
        /// Checks if a function with the given name is registered
        /// </summary>
        /// <param name="functionName">Name of the function to check</param>
        bool HasFunction(string functionName);

        /// <summary>
        /// Gets the definition of a specific function
        /// </summary>
        /// <param name="functionName">Name of the function</param>
        FunctionDefinition? GetFunctionDefinition(string functionName);

        /// <summary>
        /// Unregisters a function so it can no longer be called
        /// </summary>
        /// <param name="functionName">Name of the function to unregister</param>
        bool UnregisterFunction(string functionName);
    }
}
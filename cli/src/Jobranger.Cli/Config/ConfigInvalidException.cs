using Jobranger.Cli.Text;

namespace Jobranger.Cli.Config;

internal sealed class ConfigInvalidException(string path, IReadOnlyList<ConfigError> errors)
    : Exception($"{path} is invalid: {string.Join(errorSeparator, errors.Select(error => error.Describe(english)))}") {
    private const string errorSeparator = " | ";

    private static readonly IMessages english = new EnglishMessages();

    public string Path { get; } = path;

    public IReadOnlyList<ConfigError> Errors { get; } = errors;
}

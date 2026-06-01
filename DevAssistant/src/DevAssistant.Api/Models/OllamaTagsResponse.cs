namespace DevAssistant.Api.Models
{
    public sealed record OllamaTagsResponse(
        OllamaModel[] Models
        );

    public sealed record OllamaModel(
        string Name,
        string Model,
        long Size
        );

}

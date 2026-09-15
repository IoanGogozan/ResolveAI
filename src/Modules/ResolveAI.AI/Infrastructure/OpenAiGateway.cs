using System.ClientModel;
using System.ClientModel.Primitives;
using OpenAI;
using OpenAI.Chat;
using ResolveAI.AI.Contracts;

namespace ResolveAI.AI.Infrastructure;

internal sealed class OpenAiGateway(AiSettings settings) : ILlmGateway
{
    public string Provider => "OpenAI";
    public string Model => settings.Model;

    public async Task<ModelReply> GenerateAsync(ProposalModelInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey)) throw new ModelGatewayException("ProviderNotConfigured");
        if (ProposalProtocol.ReserveCost(input, settings) > settings.MaxInvocationCostUsd)
            throw new ProposalRequestException("InvocationBudgetExceeded");
        var client = new ChatClient(settings.Model, new ApiKeyCredential(settings.ApiKey), new OpenAIClientOptions
        {
            RetryPolicy = new ClientRetryPolicy(0), NetworkTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        });
        try
        {
            ChatCompletion completion = await client.CompleteChatAsync(
                [new SystemChatMessage(ProposalProtocol.SystemPrompt), new UserChatMessage(ProposalProtocol.SerializeInput(input))],
                new ChatCompletionOptions
                {
                    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(ProposalProtocol.SchemaVersion,
                        BinaryData.FromString(ProposalProtocol.JsonSchema), jsonSchemaIsStrict: true),
                    MaxOutputTokenCount = settings.MaxOutputTokens, StoredOutputEnabled = false, Temperature = 0
                }, ct);
            var outcome = !string.IsNullOrEmpty(completion.Refusal) ? "Refused" :
                completion.FinishReason == ChatFinishReason.Stop ? "Completed" : "Incomplete";
            return new(outcome == "Completed" ? string.Concat(completion.Content.Select(x => x.Text)) : null,
                completion.Model, outcome, completion.Usage?.InputTokenCount, completion.Usage?.OutputTokenCount);
        }
        catch (ClientResultException ex)
        {
            throw new ModelGatewayException(ex.Status switch
            {
                401 or 403 => "ProviderAuthenticationFailed", 429 => "ProviderRateLimited", _ => "ProviderFailure"
            });
        }
    }
}

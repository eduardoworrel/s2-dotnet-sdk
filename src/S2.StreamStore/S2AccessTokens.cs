using S2.StreamStore.Http;
using S2.StreamStore.Models;

namespace S2.StreamStore;

/// <summary>
/// Account-scoped helper for managing access tokens.
/// </summary>
public sealed class S2AccessTokens
{
    private readonly S2HttpClient _httpClient;
    private readonly string _accountUrl;

    internal S2AccessTokens(S2HttpClient httpClient, string accountUrl)
    {
        _httpClient = httpClient;
        _accountUrl = accountUrl;
    }

    private string TokensUrl => $"{_accountUrl}/access_tokens";

    /// <summary>
    /// List access tokens.
    /// </summary>
    /// <param name="prefix">Filter to tokens whose IDs start with this prefix.</param>
    /// <param name="startAfter">ID to start after (for pagination).</param>
    /// <param name="limit">Max results (up to 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<ListAccessTokensResponse> ListAsync(
        string? prefix = null,
        string? startAfter = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var query = BuildQuery(prefix, startAfter, limit);
        var url = string.IsNullOrEmpty(query) ? TokensUrl : $"{TokensUrl}?{query}";
        return await _httpClient.GetAsync<ListAccessTokensResponse>(url, ct);
    }

    /// <summary>
    /// List all access tokens with automatic pagination.
    /// </summary>
    public async IAsyncEnumerable<AccessTokenInfo> ListAllAsync(
        string? prefix = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        string? startAfter = null;
        bool hasMore = true;

        while (hasMore && !ct.IsCancellationRequested)
        {
            var response = await ListAsync(prefix, startAfter, 1000, ct);

            foreach (var token in response.AccessTokens)
            {
                yield return token;
                startAfter = token.Id;
            }

            hasMore = response.HasMore;
        }
    }

    /// <summary>
    /// Issue a new access token.
    /// </summary>
    /// <param name="input">Access token creation input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created access token string.</returns>
    public async Task<string> IssueAsync(IssueAccessTokenInput input, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync<IssueAccessTokenInput, IssueAccessTokenResponse>(
            TokensUrl, input, ct);
        return response.AccessToken ?? throw new InvalidOperationException("No access token returned");
    }

    /// <summary>
    /// Revoke an access token.
    /// </summary>
    /// <param name="tokenId">Access token ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RevokeAsync(string tokenId, CancellationToken ct = default)
    {
        await _httpClient.DeleteAsync($"{TokensUrl}/{Uri.EscapeDataString(tokenId)}", ct);
    }

    private static string BuildQuery(string? prefix, string? startAfter, int? limit)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(prefix))
            parts.Add($"prefix={Uri.EscapeDataString(prefix)}");
        if (!string.IsNullOrEmpty(startAfter))
            parts.Add($"start_after={Uri.EscapeDataString(startAfter)}");
        if (limit.HasValue)
            parts.Add($"limit={limit.Value}");
        return string.Join("&", parts);
    }
}

using Google.Apis.Auth.OAuth2;

namespace SmartShoppingChatBot.Application.Interface
{
    public class GoogleAccessTokenProvider
    {
        private const string CloudPlatformScope = "https://www.googleapis.com/auth/cloud-platform";

        private readonly Lazy<Task<GoogleCredential>> _credential;

        public GoogleAccessTokenProvider()
        {
            _credential = new Lazy<Task<GoogleCredential>>(
                CreateCredentialAsync,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public async Task<string> GetAccessTokenAsync(
            CancellationToken ct = default)
        {
            var credential = await _credential.Value.WaitAsync(ct);

            return await credential.UnderlyingCredential
                .GetAccessTokenForRequestAsync(
                    cancellationToken: ct);
        }

        private static async Task<GoogleCredential> CreateCredentialAsync()
        {
            var credential = await GoogleCredential.GetApplicationDefaultAsync();

            return credential.CreateScoped(CloudPlatformScope);
        }
    }
}

using Monopoly.Server.Services;

namespace Monopoly.Server.Services
{
    public static class ServiceLocator
    {
        public static readonly FirebaseApiService FirebaseApi = new FirebaseApiService();
    }
}

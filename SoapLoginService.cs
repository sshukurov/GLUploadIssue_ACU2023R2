using System;
using System.ServiceModel;
using System.Threading.Tasks;
using Velixo.Reports.AcumaticaSoap;

namespace GLUploadIssue_ACU2023R2
{
    public class SoapLoginService
    {
        public async Task<TResult> LoginAsync<TResult>(AcumaticaConnection connection, Func<ScreenSoap, Task<TResult>> worker)
        {
            BasicHttpBinding binding = new()
            {
                Name = "ScreenSoap",
                AllowCookies = true,
                MaxReceivedMessageSize = 2147483647,
                SendTimeout = TimeSpan.MaxValue,
                ReceiveTimeout = TimeSpan.MaxValue
            };

            EndpointAddress address = new($"{connection.Url}/Soap/.asmx");

            if (address.Uri.Scheme == Uri.UriSchemeHttps)
            {
                binding.Security.Mode = BasicHttpSecurityMode.Transport;
            }

            var screen = new ScreenSoapClient(binding, address);

            try
            {
                await screen.LoginAsync($"{connection.Username}@{connection.Tenant}", connection.Password);
                return await worker(screen);
            }
            finally
            {
                await screen.LogoutAsync();
            }
        }
    }
}

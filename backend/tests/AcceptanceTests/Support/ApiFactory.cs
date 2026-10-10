using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AcceptanceTests.Support;

public sealed class ApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" skips the Development-only startup migration: the hooks migrate the container themselves.
        builder
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:database", connectionString);
    }
}

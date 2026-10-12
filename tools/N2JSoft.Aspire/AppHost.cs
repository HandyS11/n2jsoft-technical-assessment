var builder = DistributedApplication.CreateBuilder(args);

// The volume keeps the database between runs. The end-to-end tests remove it to start from the seed every time.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var mainDatabase = postgres.AddDatabase("database");

var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReference(mainDatabase)
    .WaitFor(postgres)
    .WaitFor(mainDatabase);

// The webapi follows the AppHost's launch profile: under "http" it has no https endpoint.
var scalarEndpoint = webapi.GetEndpoint("https").Exists ? "https" : "http";
webapi.WithUrlForEndpoint(scalarEndpoint, _ => new()
{
    Url = "/scalar", DisplayText = "Scalar"
});

builder.AddJavaScriptApp("angular", "../../frontend", "start")
    .WithReference(webapi)
    .WaitFor(webapi)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync().ConfigureAwait(false);

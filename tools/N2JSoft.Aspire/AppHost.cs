var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var mainDatabase = postgres.AddDatabase("database");

var cache = builder.AddRedis("cache");
var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReference(cache)
    .WithReference(mainDatabase)
    .WaitFor(postgres)
    .WaitFor(mainDatabase)
    .WaitFor(cache);

builder.AddJavaScriptApp("angular", "../../frontend", "start")
    .WithReference(webapi)
    .WaitFor(webapi)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync().ConfigureAwait(false);

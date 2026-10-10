var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var mainDatabase = postgres.AddDatabase("database");

var webapi = builder.AddProject<Projects.WebApi>("webapi")
    .WithReference(mainDatabase)
    .WaitFor(postgres)
    .WaitFor(mainDatabase);

builder.AddJavaScriptApp("angular", "../../frontend", "start")
    .WithReference(webapi)
    .WaitFor(webapi)
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints();

await builder.Build().RunAsync().ConfigureAwait(false);

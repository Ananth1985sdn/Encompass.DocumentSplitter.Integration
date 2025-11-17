using Encompass.DocumentSplitter.Integration.Interfaces;
using Encompass.DocumentSplitter.Integration.Models;
using Encompass.DocumentSplitter.Integration.Services;

try 
{ 
    var builder = WebApplication.CreateBuilder(args); 
    builder.Services.Configure<EncompassSettings>(builder.Configuration.GetSection("EncompassSettings")); 
    builder.Services.AddControllers(); 
    builder.Services.AddEndpointsApiExplorer(); 
    builder.Services.AddSwaggerGen();
    builder.Services.AddHttpClient<IEncompassService, EncompassService>(client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
    });
    builder.Services.AddScoped<IEncompassDocumentUploadService, EncompassDocumentUploadService>(); 
    builder.Services.AddMemoryCache(); 
    builder.Logging.ClearProviders(); 
    builder.Logging.AddConsole(); 
    var app = builder.Build(); 
    if (app.Environment.IsDevelopment()) 
    { 
        app.UseSwagger(); 
        app.UseSwaggerUI(); 
    } 
    app.UseHttpsRedirection(); 
    app.MapControllers(); 
    app.Run(); 

} 
catch (Exception ex) 
{ 
    Console.WriteLine($"Fatal error during application startup: {ex}"); throw; 
}
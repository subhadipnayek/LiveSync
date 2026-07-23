using LiveSync.Sandbox.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SandboxOptions>(builder.Configuration.GetSection(SandboxOptions.SectionName));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

builder.Services.AddSingleton<ISandboxExecutor, CSharpSandboxExecutor>();
builder.Services.AddSingleton<IExecutionCatalog, ExecutionCatalog>();
builder.Services.AddSingleton<ISandboxExecutionService, SandboxExecutionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.MapControllers();
app.Run();

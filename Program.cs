namespace TestProject {
    public class Program {
        public static void Main(string[] args) {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers();
            
            // Configure Swagger/OpenAPI
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c => {
                var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath)) {
                    c.IncludeXmlComments(xmlPath);
                }
            });

            var app = builder.Build();

            // Validate HomeDirectory on startup
            var homeDirectory = app.Configuration["FileBrowser:HomeDirectory"];
            if (string.IsNullOrWhiteSpace(homeDirectory)) {
                throw new InvalidOperationException("FileBrowser:HomeDirectory is not configured in appsettings.json.");
            }
            if (!Directory.Exists(homeDirectory)) {
                throw new DirectoryNotFoundException($"The configured HomeDirectory does not exist: {homeDirectory}");
            }

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI(c => {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "FileBrowser API V1");
            });

            app.UseHttpsRedirection();

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.MapControllers();

            app.Run();
        }
    }
}
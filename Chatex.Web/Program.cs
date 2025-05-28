using Chatex.Core.Services;
using Chatex.Data;
using Microsoft.EntityFrameworkCore;

namespace Chatex.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
            builder.Services.AddControllers();
            builder.Services.AddDbContext<DataContext>(opt =>
                opt.UseNpgsql(connectionString, x => x.MigrationsAssembly("Chatex.Web"))
            );
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<ChatService>();
            builder.Services.AddScoped<UserService>();
            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}

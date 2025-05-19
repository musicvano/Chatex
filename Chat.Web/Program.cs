using Chat.Core.Services;
using Chat.Data;
using Microsoft.EntityFrameworkCore;

namespace Chat.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
            builder.Services.AddControllers();
            builder.Services.AddDbContext<Context>(opt =>
                opt.UseNpgsql(connectionString, x => x.MigrationsAssembly("Chat.Web"))
            );
            builder.Services.AddScoped<ChatService>();
            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Staj360.Web;
using Staj360.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(new string[] { "--environment", "Development" });
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
var app = builder.Build();

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

var dupUserId = Guid.Parse(""e4e3cf01-2210-49a8-070d-08dee306492b"");
var user = db.Users.FirstOrDefault(u => u.Id == dupUserId);
Console.WriteLine(""User with dup Id: "" + (user != null ? user.Email : ""NULL""));

var profile = db.InternProfiles.FirstOrDefault(p => p.UserId == dupUserId);
Console.WriteLine(""Profile with dup Id: "" + (profile != null ? profile.StudentNumber : ""NULL""));

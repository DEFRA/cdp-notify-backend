using Microsoft.AspNetCore.Builder;
using Environment = Defra.Cdp.Notify.Backend.Api.Config.Environment;

namespace Defra.Cdp.Notify.Backend.Tests.Config;

public class EnvironmentTest
{

   [Fact]
   public void IsNotDevModeByDefault()
   { 
       var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
       var isDev = Environment.IsDevMode(builder);
       Assert.False(isDev);
   }
}

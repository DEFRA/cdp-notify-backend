using Microsoft.AspNetCore.Builder;

namespace CdpNotifyBackend.Test.Config;

public class EnvironmentTest
{

   [Fact]
   public void IsNotDevModeByDefault()
   { 
       var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());
       var isDev = CdpNotifyBackend.Config.Environment.IsDevMode(builder);
       Assert.False(isDev);
   }
}

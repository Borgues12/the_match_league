using System;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Google;
using Owin;

[assembly: OwinStartup(typeof(juego_MVC_bomber.Startup))]

namespace juego_MVC_bomber
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Configurar autenticación por cookies
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = "ApplicationCookie",
                LoginPath = new PathString("/Account/Login"),
                ExpireTimeSpan = TimeSpan.FromHours(2),
                SlidingExpiration = true
            });

            // Cookie externa para proveedores OAuth (Google)
            app.SetDefaultSignInAsAuthenticationType("ExternalCookie");
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = "ExternalCookie",
                AuthenticationMode = AuthenticationMode.Passive,
                ExpireTimeSpan = TimeSpan.FromMinutes(5)
            });

            // Configurar Google OAuth
            var googleOptions = new GoogleOAuth2AuthenticationOptions
            {
                ClientId = "1006671733943-tmdflgr0hg6sltb2uf5bvfb6bo5urjb5.apps.googleusercontent.com",
                ClientSecret = "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",
                CallbackPath = new PathString("/signin-google")
            };

            googleOptions.Scope.Add("email");
            googleOptions.Scope.Add("profile");

            app.UseGoogleAuthentication(googleOptions);
        }
    }
}
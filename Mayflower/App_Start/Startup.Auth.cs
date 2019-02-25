using System;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
//using Microsoft.Owin.Security.Google;
using Owin;
using Mayflower.Models;
using Microsoft.Owin.Security.Facebook;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Mayflower
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit https://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            // Configure the db context, user manager and signin manager to use a single instance per request
            app.CreatePerOwinContext(ApplicationDbContext.Create);
            app.CreatePerOwinContext<ApplicationUserManager>(ApplicationUserManager.Create);
            app.CreatePerOwinContext<ApplicationSignInManager>(ApplicationSignInManager.Create);

            // Enable the application to use a cookie to store information for the signed in user
            // and to use a cookie to temporarily store information about a user logging in with a third party login provider
            // Configure the sign in cookie
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login"),
                Provider = new CookieAuthenticationProvider
                {
                    // Enables the application to validate the security stamp when the user logs in.
                    // This is a security feature which is used when you change a password or add an external login to your account.  
                    OnValidateIdentity = SecurityStampValidator.OnValidateIdentity<ApplicationUserManager, ApplicationUser>(
                        validateInterval: TimeSpan.FromMinutes(30),
                        regenerateIdentity: (manager, user) => user.GenerateUserIdentityAsync(manager))
                }
            });
            app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

            // Enables the application to temporarily store user information when they are verifying the second factor in the two-factor authentication process.
            app.UseTwoFactorSignInCookie(DefaultAuthenticationTypes.TwoFactorCookie, TimeSpan.FromMinutes(5));

            // Enables the application to remember the second login verification factor such as phone or email.
            // Once you check this option, your second step of verification during the login process will be remembered on the device where you logged in from.
            // This is similar to the RememberMe option when you log in.
            app.UseTwoFactorRememberBrowserCookie(DefaultAuthenticationTypes.TwoFactorRememberBrowserCookie);

            // Uncomment the following lines to enable logging in with third party login providers
            //app.UseMicrosoftAccountAuthentication(
            //    clientId: "",
            //    clientSecret: "");

            //app.UseTwitterAuthentication(
            //   consumerKey: "",
            //   consumerSecret: "");

            // Facebook AppId to enable FB Login
            var fbAuthOptions = new Microsoft.Owin.Security.Facebook.FacebookAuthenticationOptions
            {
                //AppId = "336626740151662",
                //AppSecret = "9ac9167d3dd4a7b97f542088327b80bd",
                AppId = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("FB.AppId"),
                AppSecret = Alphareds.Module.Common.Core.GetAppSettingValueEnhanced("FB.AppSecret"),
                Provider = new FacebookProvider
                {
                    OnAuthenticated = async context =>
                    {
                        foreach (var x in context.User)
                        {
                            if (x.Key == "birthday")
                            {
                                context.Identity.AddClaim(new Claim("dateofbirth", x.Value.ToString()));
                            }
                            else
                            {
                                context.Identity.AddClaim(new Claim(x.Key, x.Value.ToString()));
                            }
                        }
                        context.Identity.AddClaim(new Claim("fb_accecctoken", context.AccessToken));

                        await Task.FromResult(context);
                    }
                },
            };

            fbAuthOptions.Scope.Add("public_profile");
            fbAuthOptions.Scope.Add("email");
            //fbAuthOptions.Scope.Add("user_birthday");
            fbAuthOptions.Fields.Add("email");
            fbAuthOptions.Fields.Add("name");
            //fbAuthOptions.Fields.Add("user_birthday");

            app.UseFacebookAuthentication(fbAuthOptions);
            
            //app.UseGoogleAuthentication(new GoogleOAuth2AuthenticationOptions()
            //{
            //    ClientId = "",
            //    ClientSecret = ""
            //});
        }

        public class FacebookProvider : FacebookAuthenticationProvider
        {
            public override void ApplyRedirect(FacebookApplyRedirectContext context)
            {
                //To handle rerequest to give some permission
                string authType = string.Empty;
                if (context.Properties.Dictionary.ContainsKey("auth_type"))
                {
                    authType = string.Format("&auth_type={0}", context.Properties.Dictionary["auth_type"]);
                }
                //If you have popup loggin add &display=popup
                context.Response.Redirect(string.Format("{0}{1}{2}", context.RedirectUri, "&display=popup", authType));
            }
        }
    }
}
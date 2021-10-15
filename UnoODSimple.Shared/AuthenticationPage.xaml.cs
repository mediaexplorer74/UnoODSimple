using Windows.UI.Xaml;

namespace UnoODSimple
{
    public sealed partial class AuthenticationPage
    {
        private readonly OneDriveService _service;

        public AuthenticationPage()
        {
            InitializeComponent();

            _service = ((App)Application.Current).ServiceInstance;


            Loaded += (s, e) =>
            {
                var uri = _service.GetStartUri();
                
                Web.Navigate(uri);
            };


            Web.NavigationCompleted += (s, e) =>
            {
                if (_service.CheckRedirectUrl(e.Uri.AbsoluteUri))
                {
                    _service.ContinueGetTokens(e.Uri);
                }
            };


            Web.NavigationFailed += (s, e) =>
            {
                _service.ContinueGetTokens(null);
            };

        }//AuthentificationPage()
    }//AuthentificationPage class end
}//UnoODSimple namespace end
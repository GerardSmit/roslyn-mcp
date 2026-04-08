// Minimal stubs so the ASPX parser can resolve asp:* controls and HTML server controls
// in tests without requiring a real System.Web reference.

// ReSharper disable CheckNamespace
#pragma warning disable CS0067 // Event is never used

namespace System.Web.UI
{
    public class Control { }

    public class Page : Control
    {
        public bool IsPostBack { get; }
    }

    namespace HtmlControls
    {
        public class HtmlGenericControl : Control { }
        public class HtmlForm : Control { }
        public class HtmlHead : Control { }
        public class HtmlTitle : Control { }
        public class HtmlLink : Control { }
        public class HtmlImage : Control { }
    }

    namespace WebControls
    {
        public class WebControl : Control { }

        public class Label : WebControl
        {
            public string Text { get; set; } = "";
        }

        public class Button : WebControl
        {
            public string Text { get; set; } = "";
            public event EventHandler? Click;
        }

        public class TextBox : WebControl
        {
            public string Text { get; set; } = "";
            public event EventHandler? TextChanged;
        }

        public class LinkButton : WebControl
        {
            public string Text { get; set; } = "";
            public event EventHandler? Click;
            public string PostBackUrl { get; set; } = "";
        }
    }
}

namespace AspxProject
{
    public class DefaultPage : System.Web.UI.Page
    {
        protected void BtnSubmit_Click(object sender, EventArgs e) { }
    }
}

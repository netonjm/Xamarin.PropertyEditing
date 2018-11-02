using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle ("Xamarin.PropertyEditing")]
[assembly: ComVisible (false)]
[assembly: Guid ("a0b6fe73-d046-4e1c-ba9d-f20683889c5a")]

#if STRONG_NAMED
[assembly: InternalsVisibleTo ("Xamarin.PropertyEditing.Tests, PublicKey=002400000480000094000000060200000024000052534131000400000100010079159977d2d03a8e6bea7a2e74e8d1afcc93e8851974952bb480a12c9134474d04062447c37e0e68c080536fcf3c3fbe2ff9c979ce998475e506e8ce82dd5b0f350dc10e93bf2eeecf874b24770c5081dbea7447fddafa277b22de47d6ffea449674a4f9fccf84d15069089380284dbdd35f46cdff12a1bd78e4ef0065d016df")]
[assembly: InternalsVisibleTo ("Xamarin.PropertyEditing.Windows, PublicKey=002400000480000094000000060200000024000052534131000400000100010079159977d2d03a8e6bea7a2e74e8d1afcc93e8851974952bb480a12c9134474d04062447c37e0e68c080536fcf3c3fbe2ff9c979ce998475e506e8ce82dd5b0f350dc10e93bf2eeecf874b24770c5081dbea7447fddafa277b22de47d6ffea449674a4f9fccf84d15069089380284dbdd35f46cdff12a1bd78e4ef0065d016df")]
[assembly: InternalsVisibleTo ("Xamarin.PropertyEditing.Mac, PublicKey=002400000480000094000000060200000024000052534131000400000100010079159977d2d03a8e6bea7a2e74e8d1afcc93e8851974952bb480a12c9134474d04062447c37e0e68c080536fcf3c3fbe2ff9c979ce998475e506e8ce82dd5b0f350dc10e93bf2eeecf874b24770c5081dbea7447fddafa277b22de47d6ffea449674a4f9fccf84d15069089380284dbdd35f46cdff12a1bd78e4ef0065d016df")]
#else
[assembly: InternalsVisibleTo ("Xamarin.PropertyEditing.Tests")]
[assembly: InternalsVisibleTo ("Xamarin.PropertyEditing.Windows")]
[assembly: InternalsVisibleTo ("Xamarin.PropertyEditing.Mac")]
[assembly: InternalsVisibleTo ("MonoDevelop.Inspector.Core")]
[assembly: InternalsVisibleTo ("MonoDevelop.Inspector.Gtk")]
[assembly: InternalsVisibleTo ("MonoDevelop.Inspector.Mac")]
#endif

; ModuleID = 'marshal_methods.x86_64.ll'
source_filename = "marshal_methods.x86_64.ll"
target datalayout = "e-m:e-p270:32:32-p271:32:32-p272:64:64-i64:64-f80:128-n8:16:32:64-S128"
target triple = "x86_64-unknown-linux-android21"

%struct.MarshalMethodName = type {
	i64, ; uint64_t id
	ptr ; char* name
}

%struct.MarshalMethodsManagedClass = type {
	i32, ; uint32_t token
	ptr ; MonoClass klass
}

@assembly_image_cache = dso_local local_unnamed_addr global [332 x ptr] zeroinitializer, align 16

; Each entry maps hash of an assembly name to an index into the `assembly_image_cache` array
@assembly_image_cache_hashes = dso_local local_unnamed_addr constant [996 x i64] [
	i64 u0x001e58127c546039, ; 0: lib_System.Globalization.dll.so => 42
	i64 u0x0024d0f62dee05bd, ; 1: Xamarin.KotlinX.Coroutines.Core.dll => 293
	i64 u0x0071cf2d27b7d61e, ; 2: lib_Xamarin.AndroidX.SwipeRefreshLayout.dll.so => 272
	i64 u0x01109b0e4d99e61f, ; 3: System.ComponentModel.Annotations.dll => 13
	i64 u0x02123411c4e01926, ; 4: lib_Xamarin.AndroidX.Navigation.Runtime.dll.so => 260
	i64 u0x022e81ea9c46e03a, ; 5: lib_CommunityToolkit.Maui.Core.dll.so => 179
	i64 u0x0284512fad379f7e, ; 6: System.Runtime.Handles => 107
	i64 u0x02abedc11addc1ed, ; 7: lib_Mono.Android.Runtime.dll.so => 174
	i64 u0x02f55bf70672f5c8, ; 8: lib_System.IO.FileSystem.DriveInfo.dll.so => 48
	i64 u0x032267b2a94db371, ; 9: lib_Xamarin.AndroidX.AppCompat.dll.so => 214
	i64 u0x03621c804933a890, ; 10: System.Buffers => 7
	i64 u0x0399610510a38a38, ; 11: lib_System.Private.DataContractSerialization.dll.so => 88
	i64 u0x043032f1d071fae0, ; 12: ru/Microsoft.Maui.Controls.resources => 321
	i64 u0x044440a55165631e, ; 13: lib-cs-Microsoft.Maui.Controls.resources.dll.so => 299
	i64 u0x046eb1581a80c6b0, ; 14: vi/Microsoft.Maui.Controls.resources => 327
	i64 u0x047408741db2431a, ; 15: Xamarin.AndroidX.DynamicAnimation => 234
	i64 u0x0517ef04e06e9f76, ; 16: System.Net.Primitives => 72
	i64 u0x0565d18c6da3de38, ; 17: Xamarin.AndroidX.RecyclerView => 264
	i64 u0x0581db89237110e9, ; 18: lib_System.Collections.dll.so => 12
	i64 u0x05989cb940b225a9, ; 19: Microsoft.Maui.dll => 202
	i64 u0x05a1c25e78e22d87, ; 20: lib_System.Runtime.CompilerServices.Unsafe.dll.so => 104
	i64 u0x06076b5d2b581f08, ; 21: zh-HK/Microsoft.Maui.Controls.resources => 328
	i64 u0x06388ffe9f6c161a, ; 22: System.Xml.Linq.dll => 159
	i64 u0x06600c4c124cb358, ; 23: System.Configuration.dll => 19
	i64 u0x067f95c5ddab55b3, ; 24: lib_Xamarin.AndroidX.Fragment.Ktx.dll.so => 239
	i64 u0x0680a433c781bb3d, ; 25: Xamarin.AndroidX.Collection.Jvm => 221
	i64 u0x069fff96ec92a91d, ; 26: System.Xml.XPath.dll => 164
	i64 u0x070b0847e18dab68, ; 27: Xamarin.AndroidX.Emoji2.ViewsHelper.dll => 236
	i64 u0x0739448d84d3b016, ; 28: lib_Xamarin.AndroidX.VectorDrawable.dll.so => 276
	i64 u0x07469f2eecce9e85, ; 29: mscorlib.dll => 170
	i64 u0x07c57877c7ba78ad, ; 30: ru/Microsoft.Maui.Controls.resources.dll => 321
	i64 u0x07dcdc7460a0c5e4, ; 31: System.Collections.NonGeneric => 10
	i64 u0x08122e52765333c8, ; 32: lib_Microsoft.Extensions.Logging.Debug.dll.so => 197
	i64 u0x088610fc2509f69e, ; 33: lib_Xamarin.AndroidX.VectorDrawable.Animated.dll.so => 277
	i64 u0x08a7c865576bbde7, ; 34: System.Reflection.Primitives => 98
	i64 u0x08c9d051a4a817e5, ; 35: Xamarin.AndroidX.CustomView.PoolingContainer.dll => 232
	i64 u0x08f3c9788ee2153c, ; 36: Xamarin.AndroidX.DrawerLayout => 233
	i64 u0x09138715c92dba90, ; 37: lib_System.ComponentModel.Annotations.dll.so => 13
	i64 u0x0919c28b89381a0b, ; 38: lib_Microsoft.Extensions.Options.dll.so => 198
	i64 u0x092266563089ae3e, ; 39: lib_System.Collections.NonGeneric.dll.so => 10
	i64 u0x09d144a7e214d457, ; 40: System.Security.Cryptography => 129
	i64 u0x09e2b9f743db21a8, ; 41: lib_System.Reflection.Metadata.dll.so => 97
	i64 u0x0abb3e2b271edc45, ; 42: System.Threading.Channels.dll => 143
	i64 u0x0b06b1feab070143, ; 43: System.Formats.Tar => 39
	i64 u0x0b3b632c3bbee20c, ; 44: sk/Microsoft.Maui.Controls.resources => 322
	i64 u0x0b6aff547b84fbe9, ; 45: Xamarin.KotlinX.Serialization.Core.Jvm => 296
	i64 u0x0be2e1f8ce4064ed, ; 46: Xamarin.AndroidX.ViewPager => 279
	i64 u0x0c3ca6cc978e2aae, ; 47: pt-BR/Microsoft.Maui.Controls.resources => 318
	i64 u0x0c59ad9fbbd43abe, ; 48: Mono.Android => 175
	i64 u0x0c65741e86371ee3, ; 49: lib_Xamarin.Android.Glide.GifDecoder.dll.so => 208
	i64 u0x0c74af560004e816, ; 50: Microsoft.Win32.Registry.dll => 5
	i64 u0x0c7790f60165fc06, ; 51: lib_Microsoft.Maui.Essentials.dll.so => 203
	i64 u0x0c83c82812e96127, ; 52: lib_System.Net.Mail.dll.so => 68
	i64 u0x0cce4bce83380b7f, ; 53: Xamarin.AndroidX.Security.SecurityCrypto => 269
	i64 u0x0d13cd7cce4284e4, ; 54: System.Security.SecureString => 132
	i64 u0x0d63f4f73521c24f, ; 55: lib_Xamarin.AndroidX.SavedState.SavedState.Ktx.dll.so => 268
	i64 u0x0e04e702012f8463, ; 56: Xamarin.AndroidX.Emoji2 => 235
	i64 u0x0e14e73a54dda68e, ; 57: lib_System.Net.NameResolution.dll.so => 69
	i64 u0x0f37dd7a62ae99af, ; 58: lib_Xamarin.AndroidX.Collection.Ktx.dll.so => 222
	i64 u0x0f5e7abaa7cf470a, ; 59: System.Net.HttpListener => 67
	i64 u0x1001f97bbe242e64, ; 60: System.IO.UnmanagedMemoryStream => 57
	i64 u0x102a31b45304b1da, ; 61: Xamarin.AndroidX.CustomView => 231
	i64 u0x1065c4cb554c3d75, ; 62: System.IO.IsolatedStorage.dll => 52
	i64 u0x10f6cfcbcf801616, ; 63: System.IO.Compression.Brotli => 43
	i64 u0x114443cdcf2091f1, ; 64: System.Security.Cryptography.Primitives => 127
	i64 u0x11a603952763e1d4, ; 65: System.Net.Mail => 68
	i64 u0x11a70d0e1009fb11, ; 66: System.Net.WebSockets.dll => 83
	i64 u0x11f26371eee0d3c1, ; 67: lib_Xamarin.AndroidX.Lifecycle.Runtime.Ktx.dll.so => 249
	i64 u0x12128b3f59302d47, ; 68: lib_System.Xml.Serialization.dll.so => 161
	i64 u0x123639456fb056da, ; 69: System.Reflection.Emit.Lightweight.dll => 94
	i64 u0x12521e9764603eaa, ; 70: lib_System.Resources.Reader.dll.so => 101
	i64 u0x125b7f94acb989db, ; 71: Xamarin.AndroidX.RecyclerView.dll => 264
	i64 u0x12d3b63863d4ab0b, ; 72: lib_System.Threading.Overlapped.dll.so => 144
	i64 u0x134eab1061c395ee, ; 73: System.Transactions => 154
	i64 u0x138567fa954faa55, ; 74: Xamarin.AndroidX.Browser => 218
	i64 u0x13a01de0cbc3f06c, ; 75: lib-fr-Microsoft.Maui.Controls.resources.dll.so => 305
	i64 u0x13beedefb0e28a45, ; 76: lib_System.Xml.XmlDocument.dll.so => 165
	i64 u0x13f1e5e209e91af4, ; 77: lib_Java.Interop.dll.so => 172
	i64 u0x13f1e880c25d96d1, ; 78: he/Microsoft.Maui.Controls.resources => 306
	i64 u0x143d8ea60a6a4011, ; 79: Microsoft.Extensions.DependencyInjection.Abstractions => 189
	i64 u0x1497051b917530bd, ; 80: lib_System.Net.WebSockets.dll.so => 83
	i64 u0x14d612a531c79c05, ; 81: Xamarin.JSpecify.dll => 290
	i64 u0x14e68447938213b7, ; 82: Xamarin.AndroidX.Collection.Ktx.dll => 222
	i64 u0x152a448bd1e745a7, ; 83: Microsoft.Win32.Primitives => 4
	i64 u0x1557de0138c445f4, ; 84: lib_Microsoft.Win32.Registry.dll.so => 5
	i64 u0x15bdc156ed462f2f, ; 85: lib_System.IO.FileSystem.dll.so => 51
	i64 u0x15e300c2c1668655, ; 86: System.Resources.Writer.dll => 103
	i64 u0x16bf2a22df043a09, ; 87: System.IO.Pipes.dll => 56
	i64 u0x16ea2b318ad2d830, ; 88: System.Security.Cryptography.Algorithms => 122
	i64 u0x16eeae54c7ebcc08, ; 89: System.Reflection.dll => 100
	i64 u0x17125c9a85b4929f, ; 90: lib_netstandard.dll.so => 171
	i64 u0x1716866f7416792e, ; 91: lib_System.Security.AccessControl.dll.so => 120
	i64 u0x174f71c46216e44a, ; 92: Xamarin.KotlinX.Coroutines.Core => 293
	i64 u0x1752c12f1e1fc00c, ; 93: System.Core => 21
	i64 u0x17b56e25558a5d36, ; 94: lib-hu-Microsoft.Maui.Controls.resources.dll.so => 309
	i64 u0x17f9358913beb16a, ; 95: System.Text.Encodings.Web => 139
	i64 u0x1809fb23f29ba44a, ; 96: lib_System.Reflection.TypeExtensions.dll.so => 99
	i64 u0x18402a709e357f3b, ; 97: lib_Xamarin.KotlinX.Serialization.Core.Jvm.dll.so => 296
	i64 u0x18a9befae51bb361, ; 98: System.Net.WebClient => 79
	i64 u0x18f0ce884e87d89a, ; 99: nb/Microsoft.Maui.Controls.resources.dll => 315
	i64 u0x19777fba3c41b398, ; 100: Xamarin.AndroidX.Startup.StartupRuntime.dll => 271
	i64 u0x19a4c090f14ebb66, ; 101: System.Security.Claims => 121
	i64 u0x1a91866a319e9259, ; 102: lib_System.Collections.Concurrent.dll.so => 8
	i64 u0x1aac34d1917ba5d3, ; 103: lib_System.dll.so => 168
	i64 u0x1aad60783ffa3e5b, ; 104: lib-th-Microsoft.Maui.Controls.resources.dll.so => 324
	i64 u0x1aea8f1c3b282172, ; 105: lib_System.Net.Ping.dll.so => 71
	i64 u0x1b4b7a1d0d265fa2, ; 106: Xamarin.Android.Glide.DiskLruCache => 207
	i64 u0x1bbdb16cfa73e785, ; 107: Xamarin.AndroidX.Lifecycle.Runtime.Ktx.Android => 250
	i64 u0x1bc766e07b2b4241, ; 108: Xamarin.AndroidX.ResourceInspection.Annotation.dll => 265
	i64 u0x1c5217a9e4973753, ; 109: lib_Microsoft.Extensions.FileProviders.Physical.dll.so => 192
	i64 u0x1c753b5ff15bce1b, ; 110: Mono.Android.Runtime.dll => 174
	i64 u0x1cd47467799d8250, ; 111: System.Threading.Tasks.dll => 148
	i64 u0x1d23eafdc6dc346c, ; 112: System.Globalization.Calendars.dll => 40
	i64 u0x1da4110562816681, ; 113: Xamarin.AndroidX.Security.SecurityCrypto.dll => 269
	i64 u0x1db6820994506bf5, ; 114: System.IO.FileSystem.AccessControl.dll => 47
	i64 u0x1dbb0c2c6a999acb, ; 115: System.Diagnostics.StackTrace => 30
	i64 u0x1e3d87657e9659bc, ; 116: Xamarin.AndroidX.Navigation.UI => 262
	i64 u0x1e71143913d56c10, ; 117: lib-ko-Microsoft.Maui.Controls.resources.dll.so => 313
	i64 u0x1e7c31185e2fb266, ; 118: lib_System.Threading.Tasks.Parallel.dll.so => 147
	i64 u0x1ed8fcce5e9b50a0, ; 119: Microsoft.Extensions.Options.dll => 198
	i64 u0x1f055d15d807e1b2, ; 120: System.Xml.XmlSerializer => 166
	i64 u0x1f1ed22c1085f044, ; 121: lib_System.Diagnostics.FileVersionInfo.dll.so => 28
	i64 u0x1f61df9c5b94d2c1, ; 122: lib_System.Numerics.dll.so => 86
	i64 u0x1f750bb5421397de, ; 123: lib_Xamarin.AndroidX.Tracing.Tracing.dll.so => 273
	i64 u0x20237ea48006d7a8, ; 124: lib_System.Net.WebClient.dll.so => 79
	i64 u0x209375905fcc1bad, ; 125: lib_System.IO.Compression.Brotli.dll.so => 43
	i64 u0x20fab3cf2dfbc8df, ; 126: lib_System.Diagnostics.Process.dll.so => 29
	i64 u0x2110167c128cba15, ; 127: System.Globalization => 42
	i64 u0x21419508838f7547, ; 128: System.Runtime.CompilerServices.VisualC => 105
	i64 u0x2174319c0d835bc9, ; 129: System.Runtime => 119
	i64 u0x2198e5bc8b7153fa, ; 130: Xamarin.AndroidX.Annotation.Experimental.dll => 212
	i64 u0x219ea1b751a4dee4, ; 131: lib_System.IO.Compression.ZipFile.dll.so => 45
	i64 u0x21cc7e445dcd5469, ; 132: System.Reflection.Emit.ILGeneration => 93
	i64 u0x220fd4f2e7c48170, ; 133: th/Microsoft.Maui.Controls.resources => 324
	i64 u0x224538d85ed15a82, ; 134: System.IO.Pipes => 56
	i64 u0x22908438c6bed1af, ; 135: lib_System.Threading.Timer.dll.so => 151
	i64 u0x237be844f1f812c7, ; 136: System.Threading.Thread.dll => 149
	i64 u0x23852b3bdc9f7096, ; 137: System.Resources.ResourceManager => 102
	i64 u0x23986dd7e5d4fc01, ; 138: System.IO.FileSystem.Primitives.dll => 49
	i64 u0x2407aef2bbe8fadf, ; 139: System.Console => 20
	i64 u0x240abe014b27e7d3, ; 140: Xamarin.AndroidX.Core.dll => 227
	i64 u0x247619fe4413f8bf, ; 141: System.Runtime.Serialization.Primitives.dll => 116
	i64 u0x24de8d301281575e, ; 142: Xamarin.Android.Glide => 205
	i64 u0x252073cc3caa62c2, ; 143: fr/Microsoft.Maui.Controls.resources.dll => 305
	i64 u0x256b8d41255f01b1, ; 144: Xamarin.Google.Crypto.Tink.Android => 286
	i64 u0x2662c629b96b0b30, ; 145: lib_Xamarin.Kotlin.StdLib.dll.so => 291
	i64 u0x268c1439f13bcc29, ; 146: lib_Microsoft.Extensions.Primitives.dll.so => 199
	i64 u0x26a670e154a9c54b, ; 147: System.Reflection.Extensions.dll => 96
	i64 u0x26d077d9678fe34f, ; 148: System.IO.dll => 58
	i64 u0x273f3515de5faf0d, ; 149: id/Microsoft.Maui.Controls.resources.dll => 310
	i64 u0x2742545f9094896d, ; 150: hr/Microsoft.Maui.Controls.resources => 308
	i64 u0x274d85d83ad40513, ; 151: lib_Xamarin.AndroidX.Window.WindowCore.dll.so => 282
	i64 u0x2759af78ab94d39b, ; 152: System.Net.WebSockets => 83
	i64 u0x27b2b16f3e9de038, ; 153: Xamarin.Google.Crypto.Tink.Android.dll => 286
	i64 u0x27b410442fad6cf1, ; 154: Java.Interop.dll => 172
	i64 u0x27b97e0d52c3034a, ; 155: System.Diagnostics.Debug => 26
	i64 u0x2801845a2c71fbfb, ; 156: System.Net.Primitives.dll => 72
	i64 u0x286835e259162700, ; 157: lib_Xamarin.AndroidX.ProfileInstaller.ProfileInstaller.dll.so => 263
	i64 u0x28e52865585a1ebe, ; 158: Microsoft.Extensions.Diagnostics.Abstractions.dll => 190
	i64 u0x2949f3617a02c6b2, ; 159: Xamarin.AndroidX.ExifInterface => 237
	i64 u0x29aeab763a527e52, ; 160: lib_Xamarin.AndroidX.Navigation.Common.Android.dll.so => 258
	i64 u0x2a128783efe70ba0, ; 161: uk/Microsoft.Maui.Controls.resources.dll => 326
	i64 u0x2a3b095612184159, ; 162: lib_System.Net.NetworkInformation.dll.so => 70
	i64 u0x2a6507a5ffabdf28, ; 163: System.Diagnostics.TraceSource.dll => 33
	i64 u0x2ad156c8e1354139, ; 164: fi/Microsoft.Maui.Controls.resources => 304
	i64 u0x2ad5d6b13b7a3e04, ; 165: System.ComponentModel.DataAnnotations.dll => 14
	i64 u0x2af298f63581d886, ; 166: System.Text.RegularExpressions.dll => 141
	i64 u0x2afc1c4f898552ee, ; 167: lib_System.Formats.Asn1.dll.so => 38
	i64 u0x2b148910ed40fbf9, ; 168: zh-Hant/Microsoft.Maui.Controls.resources.dll => 330
	i64 u0x2b4d4904cebfa4e9, ; 169: Microsoft.Extensions.FileSystemGlobbing => 193
	i64 u0x2b6989d78cba9a15, ; 170: Xamarin.AndroidX.Concurrent.Futures.dll => 223
	i64 u0x2c8bd14bb93a7d82, ; 171: lib-pl-Microsoft.Maui.Controls.resources.dll.so => 317
	i64 u0x2cbd9262ca785540, ; 172: lib_System.Text.Encoding.CodePages.dll.so => 136
	i64 u0x2cc9e1fed6257257, ; 173: lib_System.Reflection.Emit.Lightweight.dll.so => 94
	i64 u0x2cd723e9fe623c7c, ; 174: lib_System.Private.Xml.Linq.dll.so => 90
	i64 u0x2d169d318a968379, ; 175: System.Threading.dll => 152
	i64 u0x2d20145f27cfc1d2, ; 176: Xamarin.AndroidX.Window.WindowCore.Jvm.dll => 283
	i64 u0x2d47774b7d993f59, ; 177: sv/Microsoft.Maui.Controls.resources.dll => 323
	i64 u0x2d5ffcae1ad0aaca, ; 178: System.Data.dll => 24
	i64 u0x2db915caf23548d2, ; 179: System.Text.Json.dll => 140
	i64 u0x2dcaa0bb15a4117a, ; 180: System.IO.UnmanagedMemoryStream.dll => 57
	i64 u0x2e2ced2c3c6a1edc, ; 181: lib_System.Threading.AccessControl.dll.so => 142
	i64 u0x2e5a40c319acb800, ; 182: System.IO.FileSystem => 51
	i64 u0x2e6f1f226821322a, ; 183: el/Microsoft.Maui.Controls.resources.dll => 302
	i64 u0x2f02f94df3200fe5, ; 184: System.Diagnostics.Process => 29
	i64 u0x2f2e98e1c89b1aff, ; 185: System.Xml.ReaderWriter => 160
	i64 u0x2f5911d9ba814e4e, ; 186: System.Diagnostics.Tracing => 34
	i64 u0x2f84070a459bc31f, ; 187: lib_System.Xml.dll.so => 167
	i64 u0x309ee9eeec09a71e, ; 188: lib_Xamarin.AndroidX.Fragment.dll.so => 238
	i64 u0x30c6dda129408828, ; 189: System.IO.IsolatedStorage => 52
	i64 u0x31195fef5d8fb552, ; 190: _Microsoft.Android.Resource.Designer.dll => 331
	i64 u0x312c8ed623cbfc8d, ; 191: Xamarin.AndroidX.Window.dll => 281
	i64 u0x31496b779ed0663d, ; 192: lib_System.Reflection.DispatchProxy.dll.so => 92
	i64 u0x32243413e774362a, ; 193: Xamarin.AndroidX.CardView.dll => 219
	i64 u0x3235427f8d12dae1, ; 194: lib_System.Drawing.Primitives.dll.so => 35
	i64 u0x329753a17a517811, ; 195: fr/Microsoft.Maui.Controls.resources => 305
	i64 u0x32aa989ff07a84ff, ; 196: lib_System.Xml.ReaderWriter.dll.so => 160
	i64 u0x33642d5508314e46, ; 197: Microsoft.Extensions.FileSystemGlobbing.dll => 193
	i64 u0x33829542f112d59b, ; 198: System.Collections.Immutable => 9
	i64 u0x33a31443733849fe, ; 199: lib-es-Microsoft.Maui.Controls.resources.dll.so => 303
	i64 u0x341abc357fbb4ebf, ; 200: lib_System.Net.Sockets.dll.so => 78
	i64 u0x346a212343615ac5, ; 201: lib_System.Linq.AsyncEnumerable.dll.so => 59
	i64 u0x3496c1e2dcaf5ecc, ; 202: lib_System.IO.Pipes.AccessControl.dll.so => 55
	i64 u0x34dfd74fe2afcf37, ; 203: Microsoft.Maui => 202
	i64 u0x34e292762d9615df, ; 204: cs/Microsoft.Maui.Controls.resources.dll => 299
	i64 u0x3508234247f48404, ; 205: Microsoft.Maui.Controls => 200
	i64 u0x353590da528c9d22, ; 206: System.ComponentModel.Annotations => 13
	i64 u0x3549870798b4cd30, ; 207: lib_Xamarin.AndroidX.ViewPager2.dll.so => 280
	i64 u0x355282fc1c909694, ; 208: Microsoft.Extensions.Configuration => 182
	i64 u0x3552fc5d578f0fbf, ; 209: Xamarin.AndroidX.Arch.Core.Common => 216
	i64 u0x355c649948d55d97, ; 210: lib_System.Runtime.Intrinsics.dll.so => 111
	i64 u0x35ea9d1c6834bc8c, ; 211: Xamarin.AndroidX.Lifecycle.ViewModel.Ktx.dll => 253
	i64 u0x3628ab68db23a01a, ; 212: lib_System.Diagnostics.Tools.dll.so => 32
	i64 u0x3673b042508f5b6b, ; 213: lib_System.Runtime.Extensions.dll.so => 106
	i64 u0x36740f1a8ecdc6c4, ; 214: System.Numerics => 86
	i64 u0x36b2b50fdf589ae2, ; 215: System.Reflection.Emit.Lightweight => 94
	i64 u0x36cada77dc79928b, ; 216: System.IO.MemoryMappedFiles => 53
	i64 u0x374ef46b06791af6, ; 217: System.Reflection.Primitives.dll => 98
	i64 u0x376bf93e521a5417, ; 218: lib_Xamarin.Jetbrains.Annotations.dll.so => 289
	i64 u0x37bc29f3183003b6, ; 219: lib_System.IO.dll.so => 58
	i64 u0x380134e03b1e160a, ; 220: System.Collections.Immutable.dll => 9
	i64 u0x38049b5c59b39324, ; 221: System.Runtime.CompilerServices.Unsafe => 104
	i64 u0x385c17636bb6fe6e, ; 222: Xamarin.AndroidX.CustomView.dll => 231
	i64 u0x38869c811d74050e, ; 223: System.Net.NameResolution.dll => 69
	i64 u0x393c226616977fdb, ; 224: lib_Xamarin.AndroidX.ViewPager.dll.so => 279
	i64 u0x395e37c3334cf82a, ; 225: lib-ca-Microsoft.Maui.Controls.resources.dll.so => 298
	i64 u0x39c3107c28752af1, ; 226: lib_Microsoft.Extensions.FileProviders.Abstractions.dll.so => 191
	i64 u0x3ab5859054645f72, ; 227: System.Security.Cryptography.Primitives.dll => 127
	i64 u0x3ad75090c3fac0e9, ; 228: lib_Xamarin.AndroidX.ResourceInspection.Annotation.dll.so => 265
	i64 u0x3ae44ac43a1fbdbb, ; 229: System.Runtime.Serialization => 118
	i64 u0x3b860f9932505633, ; 230: lib_System.Text.Encoding.Extensions.dll.so => 137
	i64 u0x3be99b43dd39dd37, ; 231: Xamarin.AndroidX.SavedState.SavedState.Android => 267
	i64 u0x3c3aafb6b3a00bf6, ; 232: lib_System.Security.Cryptography.X509Certificates.dll.so => 128
	i64 u0x3c4049146b59aa90, ; 233: System.Runtime.InteropServices.JavaScript => 108
	i64 u0x3c7c495f58ac5ee9, ; 234: Xamarin.Kotlin.StdLib => 291
	i64 u0x3c7e5ed3d5db71bb, ; 235: System.Security => 133
	i64 u0x3cd9d281d402eb9b, ; 236: Xamarin.AndroidX.Browser.dll => 218
	i64 u0x3d1c50cc001a991e, ; 237: Xamarin.Google.Guava.ListenableFuture.dll => 288
	i64 u0x3d2b1913edfc08d7, ; 238: lib_System.Threading.ThreadPool.dll.so => 150
	i64 u0x3d46f0b995082740, ; 239: System.Xml.Linq => 159
	i64 u0x3d8a8f400514a790, ; 240: Xamarin.AndroidX.Fragment.Ktx.dll => 239
	i64 u0x3d9c2a242b040a50, ; 241: lib_Xamarin.AndroidX.Core.dll.so => 227
	i64 u0x3db495de2204755c, ; 242: Microsoft.Extensions.Configuration.FileExtensions => 187
	i64 u0x3dbb6b9f5ab90fa7, ; 243: lib_Xamarin.AndroidX.DynamicAnimation.dll.so => 234
	i64 u0x3e5441657549b213, ; 244: Xamarin.AndroidX.ResourceInspection.Annotation => 265
	i64 u0x3e57d4d195c53c2e, ; 245: System.Reflection.TypeExtensions => 99
	i64 u0x3e616ab4ed1f3f15, ; 246: lib_System.Data.dll.so => 24
	i64 u0x3f1d226e6e06db7e, ; 247: Xamarin.AndroidX.SlidingPaneLayout.dll => 270
	i64 u0x3f510adf788828dd, ; 248: System.Threading.Tasks.Extensions => 146
	i64 u0x3f6f5914291cdcf7, ; 249: Microsoft.Extensions.Hosting.Abstractions => 194
	i64 u0x407a10bb4bf95829, ; 250: lib_Xamarin.AndroidX.Navigation.Common.dll.so => 257
	i64 u0x40c98b6bd77346d4, ; 251: Microsoft.VisualBasic.dll => 3
	i64 u0x41833cf766d27d96, ; 252: mscorlib => 170
	i64 u0x41cab042be111c34, ; 253: lib_Xamarin.AndroidX.AppCompat.AppCompatResources.dll.so => 215
	i64 u0x423a9ecc4d905a88, ; 254: lib_System.Resources.ResourceManager.dll.so => 102
	i64 u0x423bf51ae7def810, ; 255: System.Xml.XPath => 164
	i64 u0x42462ff15ddba223, ; 256: System.Resources.Reader.dll => 101
	i64 u0x4291015ff4e5ef71, ; 257: Xamarin.AndroidX.Core.ViewTree.dll => 229
	i64 u0x42a31b86e6ccc3f0, ; 258: System.Diagnostics.Contracts => 25
	i64 u0x430e95b891249788, ; 259: lib_System.Reflection.Emit.dll.so => 95
	i64 u0x43375950ec7c1b6a, ; 260: netstandard.dll => 171
	i64 u0x434c4e1d9284cdae, ; 261: Mono.Android.dll => 175
	i64 u0x43505013578652a0, ; 262: lib_Xamarin.AndroidX.Activity.Ktx.dll.so => 210
	i64 u0x437d06c381ed575a, ; 263: lib_Microsoft.VisualBasic.dll.so => 3
	i64 u0x43950f84de7cc79a, ; 264: pl/Microsoft.Maui.Controls.resources.dll => 317
	i64 u0x43c077442b230f64, ; 265: Xamarin.AndroidX.Tracing.Tracing.Android => 274
	i64 u0x43e8ca5bc927ff37, ; 266: lib_Xamarin.AndroidX.Emoji2.ViewsHelper.dll.so => 236
	i64 u0x448bd33429269b19, ; 267: Microsoft.CSharp => 1
	i64 u0x4499fa3c8e494654, ; 268: lib_System.Runtime.Serialization.Primitives.dll.so => 116
	i64 u0x4515080865a951a5, ; 269: Xamarin.Kotlin.StdLib.dll => 291
	i64 u0x4545802489b736b9, ; 270: Xamarin.AndroidX.Fragment.Ktx => 239
	i64 u0x454b4d1e66bb783c, ; 271: Xamarin.AndroidX.Lifecycle.Process => 246
	i64 u0x45c40276a42e283e, ; 272: System.Diagnostics.TraceSource => 33
	i64 u0x45d443f2a29adc37, ; 273: System.AppContext.dll => 6
	i64 u0x46a4213bc97fe5ae, ; 274: lib-ru-Microsoft.Maui.Controls.resources.dll.so => 321
	i64 u0x47358bd471172e1d, ; 275: lib_System.Xml.Linq.dll.so => 159
	i64 u0x47daf4e1afbada10, ; 276: pt/Microsoft.Maui.Controls.resources => 319
	i64 u0x480c0a47dd42dd81, ; 277: lib_System.IO.MemoryMappedFiles.dll.so => 53
	i64 u0x49e952f19a4e2022, ; 278: System.ObjectModel => 87
	i64 u0x49f9e6948a8131e4, ; 279: lib_Xamarin.AndroidX.VersionedParcelable.dll.so => 278
	i64 u0x4a0b4d28cc5945ae, ; 280: Microsoft.Extensions.Configuration.CommandLine => 185
	i64 u0x4a5667b2462a664b, ; 281: lib_Xamarin.AndroidX.Navigation.UI.dll.so => 262
	i64 u0x4a7a18981dbd56bc, ; 282: System.IO.Compression.FileSystem.dll => 44
	i64 u0x4aa5c60350917c06, ; 283: lib_Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx.dll.so => 245
	i64 u0x4b07a0ed0ab33ff4, ; 284: System.Runtime.Extensions.dll => 106
	i64 u0x4b576d47ac054f3c, ; 285: System.IO.FileSystem.AccessControl => 47
	i64 u0x4b7b6532ded934b7, ; 286: System.Text.Json => 140
	i64 u0x4c2029a97af23a8d, ; 287: Xamarin.AndroidX.Lifecycle.ViewModelSavedState.Android => 255
	i64 u0x4c7755cf07ad2d5f, ; 288: System.Net.Http.Json.dll => 65
	i64 u0x4cc5f15266470798, ; 289: lib_Xamarin.AndroidX.Loader.dll.so => 256
	i64 u0x4cf6f67dc77aacd2, ; 290: System.Net.NetworkInformation.dll => 70
	i64 u0x4d3183dd245425d4, ; 291: System.Net.WebSockets.Client.dll => 82
	i64 u0x4d479f968a05e504, ; 292: System.Linq.Expressions.dll => 60
	i64 u0x4d55a010ffc4faff, ; 293: System.Private.Xml => 91
	i64 u0x4d5cbe77561c5b2e, ; 294: System.Web.dll => 157
	i64 u0x4d77512dbd86ee4c, ; 295: lib_Xamarin.AndroidX.Arch.Core.Common.dll.so => 216
	i64 u0x4d7793536e79c309, ; 296: System.ServiceProcess => 135
	i64 u0x4d95fccc1f67c7ca, ; 297: System.Runtime.Loader.dll => 112
	i64 u0x4db014bf0ff1c9c1, ; 298: System.Linq.AsyncEnumerable => 59
	i64 u0x4dcf44c3c9b076a2, ; 299: it/Microsoft.Maui.Controls.resources.dll => 311
	i64 u0x4dd9247f1d2c3235, ; 300: Xamarin.AndroidX.Loader.dll => 256
	i64 u0x4e2aeee78e2c4a87, ; 301: Xamarin.AndroidX.ProfileInstaller.ProfileInstaller => 263
	i64 u0x4e32f00cb0937401, ; 302: Mono.Android.Runtime => 174
	i64 u0x4e5eea4668ac2b18, ; 303: System.Text.Encoding.CodePages => 136
	i64 u0x4ebd0c4b82c5eefc, ; 304: lib_System.Threading.Channels.dll.so => 143
	i64 u0x4ee8eaa9c9c1151a, ; 305: System.Globalization.Calendars => 40
	i64 u0x4f21ee6ef9eb527e, ; 306: ca/Microsoft.Maui.Controls.resources => 298
	i64 u0x4fdc964ec1888e25, ; 307: lib_Microsoft.Extensions.Configuration.Binder.dll.so => 184
	i64 u0x5037f0be3c28c7a3, ; 308: lib_Microsoft.Maui.Controls.dll.so => 200
	i64 u0x50c3a29b21050d45, ; 309: System.Linq.Parallel.dll => 61
	i64 u0x5112ed116d87baf8, ; 310: CommunityToolkit.Mvvm => 180
	i64 u0x5116b21580ae6eb0, ; 311: Microsoft.Extensions.Configuration.Binder.dll => 184
	i64 u0x5131bbe80989093f, ; 312: Xamarin.AndroidX.Lifecycle.ViewModel.Android.dll => 252
	i64 u0x516324a5050a7e3c, ; 313: System.Net.WebProxy => 81
	i64 u0x516d6f0b21a303de, ; 314: lib_System.Diagnostics.Contracts.dll.so => 25
	i64 u0x51bb8a2afe774e32, ; 315: System.Drawing => 36
	i64 u0x5247c5c32a4140f0, ; 316: System.Resources.Reader => 101
	i64 u0x526bb15e3c386364, ; 317: Xamarin.AndroidX.Lifecycle.Runtime.Ktx.dll => 249
	i64 u0x526ce79eb8e90527, ; 318: lib_System.Net.Primitives.dll.so => 72
	i64 u0x52829f00b4467c38, ; 319: lib_System.Data.Common.dll.so => 22
	i64 u0x529ffe06f39ab8db, ; 320: Xamarin.AndroidX.Core => 227
	i64 u0x52ff996554dbf352, ; 321: Microsoft.Maui.Graphics => 204
	i64 u0x535f7e40e8fef8af, ; 322: lib-sk-Microsoft.Maui.Controls.resources.dll.so => 322
	i64 u0x53978aac584c666e, ; 323: lib_System.Security.Cryptography.Cng.dll.so => 123
	i64 u0x53a96d5c86c9e194, ; 324: System.Net.NetworkInformation => 70
	i64 u0x53be1038a61e8d44, ; 325: System.Runtime.InteropServices.RuntimeInformation.dll => 109
	i64 u0x53c3014b9437e684, ; 326: lib-zh-HK-Microsoft.Maui.Controls.resources.dll.so => 328
	i64 u0x5435e6f049e9bc37, ; 327: System.Security.Claims.dll => 121
	i64 u0x54795225dd1587af, ; 328: lib_System.Runtime.dll.so => 119
	i64 u0x547a34f14e5f6210, ; 329: Xamarin.AndroidX.Lifecycle.Common.dll => 241
	i64 u0x54b851bc9b470503, ; 330: Xamarin.AndroidX.Navigation.Common.Android => 258
	i64 u0x556e8b63b660ab8b, ; 331: Xamarin.AndroidX.Lifecycle.Common.Jvm.dll => 242
	i64 u0x5588627c9a108ec9, ; 332: System.Collections.Specialized => 11
	i64 u0x55a898e4f42e3fae, ; 333: Microsoft.VisualBasic.Core.dll => 2
	i64 u0x55fa0c610fe93bb1, ; 334: lib_System.Security.Cryptography.OpenSsl.dll.so => 126
	i64 u0x56442b99bc64bb47, ; 335: System.Runtime.Serialization.Xml.dll => 117
	i64 u0x56a8b26e1aeae27b, ; 336: System.Threading.Tasks.Dataflow => 145
	i64 u0x56f932d61e93c07f, ; 337: System.Globalization.Extensions => 41
	i64 u0x571c5cfbec5ae8e2, ; 338: System.Private.Uri => 89
	i64 u0x576499c9f52fea31, ; 339: Xamarin.AndroidX.Annotation => 211
	i64 u0x579a06fed6eec900, ; 340: System.Private.CoreLib.dll => 177
	i64 u0x57adda3c951abb33, ; 341: Microsoft.Extensions.Hosting.Abstractions.dll => 194
	i64 u0x57c542c14049b66d, ; 342: System.Diagnostics.DiagnosticSource => 27
	i64 u0x581a8bd5cfda563e, ; 343: System.Threading.Timer => 151
	i64 u0x584ac38e21d2fde1, ; 344: Microsoft.Extensions.Configuration.Binder => 184
	i64 u0x58601b2dda4a27b9, ; 345: lib-ja-Microsoft.Maui.Controls.resources.dll.so => 312
	i64 u0x58688d9af496b168, ; 346: Microsoft.Extensions.DependencyInjection.dll => 188
	i64 u0x588c167a79db6bfb, ; 347: lib_Xamarin.Google.ErrorProne.Annotations.dll.so => 287
	i64 u0x5906028ae5151104, ; 348: Xamarin.AndroidX.Activity.Ktx => 210
	i64 u0x595a356d23e8da9a, ; 349: lib_Microsoft.CSharp.dll.so => 1
	i64 u0x59f9e60b9475085f, ; 350: lib_Xamarin.AndroidX.Annotation.Experimental.dll.so => 212
	i64 u0x5a745f5101a75527, ; 351: lib_System.IO.Compression.FileSystem.dll.so => 44
	i64 u0x5a89a886ae30258d, ; 352: lib_Xamarin.AndroidX.CoordinatorLayout.dll.so => 226
	i64 u0x5a8f6699f4a1caa9, ; 353: lib_System.Threading.dll.so => 152
	i64 u0x5ae9cd33b15841bf, ; 354: System.ComponentModel => 18
	i64 u0x5b54391bdc6fcfe6, ; 355: System.Private.DataContractSerialization => 88
	i64 u0x5b5f0e240a06a2a2, ; 356: da/Microsoft.Maui.Controls.resources.dll => 300
	i64 u0x5b8109e8e14c5e3e, ; 357: System.Globalization.Extensions.dll => 41
	i64 u0x5bddd04d72a9e350, ; 358: Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx => 245
	i64 u0x5bdf16b09da116ab, ; 359: Xamarin.AndroidX.Collection => 220
	i64 u0x5c019d5266093159, ; 360: lib_Xamarin.AndroidX.Lifecycle.Runtime.Ktx.Android.dll.so => 250
	i64 u0x5c30a4a35f9cc8c4, ; 361: lib_System.Reflection.Extensions.dll.so => 96
	i64 u0x5c393624b8176517, ; 362: lib_Microsoft.Extensions.Logging.dll.so => 195
	i64 u0x5c53c29f5073b0c9, ; 363: System.Diagnostics.FileVersionInfo => 28
	i64 u0x5c87463c575c7616, ; 364: lib_System.Globalization.Extensions.dll.so => 41
	i64 u0x5d0a4a29b02d9d3c, ; 365: System.Net.WebHeaderCollection.dll => 80
	i64 u0x5d40c9b15181641f, ; 366: lib_Xamarin.AndroidX.Emoji2.dll.so => 235
	i64 u0x5d6ca10d35e9485b, ; 367: lib_Xamarin.AndroidX.Concurrent.Futures.dll.so => 223
	i64 u0x5d7ec76c1c703055, ; 368: System.Threading.Tasks.Parallel => 147
	i64 u0x5db0cbbd1028510e, ; 369: lib_System.Runtime.InteropServices.dll.so => 110
	i64 u0x5db30905d3e5013b, ; 370: Xamarin.AndroidX.Collection.Jvm.dll => 221
	i64 u0x5e467bc8f09ad026, ; 371: System.Collections.Specialized.dll => 11
	i64 u0x5e5173b3208d97e7, ; 372: System.Runtime.Handles.dll => 107
	i64 u0x5ea92fdb19ec8c4c, ; 373: System.Text.Encodings.Web.dll => 139
	i64 u0x5eb8046dd40e9ac3, ; 374: System.ComponentModel.Primitives => 16
	i64 u0x5ec272d219c9aba4, ; 375: System.Security.Cryptography.Csp.dll => 124
	i64 u0x5eee1376d94c7f5e, ; 376: System.Net.HttpListener.dll => 67
	i64 u0x5f36ccf5c6a57e24, ; 377: System.Xml.ReaderWriter.dll => 160
	i64 u0x5f4294b9b63cb842, ; 378: System.Data.Common => 22
	i64 u0x5f9a2d823f664957, ; 379: lib-el-Microsoft.Maui.Controls.resources.dll.so => 302
	i64 u0x5fa6da9c3cd8142a, ; 380: lib_Xamarin.KotlinX.Serialization.Core.dll.so => 295
	i64 u0x5fac98e0b37a5b9d, ; 381: System.Runtime.CompilerServices.Unsafe.dll => 104
	i64 u0x609f4b7b63d802d4, ; 382: lib_Microsoft.Extensions.DependencyInjection.dll.so => 188
	i64 u0x60cd4e33d7e60134, ; 383: Xamarin.KotlinX.Coroutines.Core.Jvm => 294
	i64 u0x60f62d786afcf130, ; 384: System.Memory => 64
	i64 u0x61bb78c89f867353, ; 385: System.IO => 58
	i64 u0x61be8d1299194243, ; 386: Microsoft.Maui.Controls.Xaml => 201
	i64 u0x61d2cba29557038f, ; 387: de/Microsoft.Maui.Controls.resources => 301
	i64 u0x61d88f399afb2f45, ; 388: lib_System.Runtime.Loader.dll.so => 112
	i64 u0x622eef6f9e59068d, ; 389: System.Private.CoreLib => 177
	i64 u0x639fb99a7bef11de, ; 390: Xamarin.AndroidX.Navigation.Runtime.Android.dll => 261
	i64 u0x63d5e3aa4ef9b931, ; 391: Xamarin.KotlinX.Coroutines.Android.dll => 292
	i64 u0x63f1f6883c1e23c2, ; 392: lib_System.Collections.Immutable.dll.so => 9
	i64 u0x6400f68068c1e9f1, ; 393: Xamarin.Google.Android.Material.dll => 284
	i64 u0x640e3b14dbd325c2, ; 394: System.Security.Cryptography.Algorithms.dll => 122
	i64 u0x64587004560099b9, ; 395: System.Reflection => 100
	i64 u0x64b1529a438a3c45, ; 396: lib_System.Runtime.Handles.dll.so => 107
	i64 u0x64b61dd9da8a4d57, ; 397: System.Net.ServerSentEvents.dll => 76
	i64 u0x6565fba2cd8f235b, ; 398: Xamarin.AndroidX.Lifecycle.ViewModel.Ktx => 253
	i64 u0x658f524e4aba7dad, ; 399: CommunityToolkit.Maui.dll => 178
	i64 u0x65ecac39144dd3cc, ; 400: Microsoft.Maui.Controls.dll => 200
	i64 u0x65ece51227bfa724, ; 401: lib_System.Runtime.Numerics.dll.so => 113
	i64 u0x661722438787b57f, ; 402: Xamarin.AndroidX.Annotation.Jvm.dll => 213
	i64 u0x6679b2337ee6b22a, ; 403: lib_System.IO.FileSystem.Primitives.dll.so => 49
	i64 u0x667c66a03dd97d40, ; 404: System.Linq.AsyncEnumerable.dll => 59
	i64 u0x6692e924eade1b29, ; 405: lib_System.Console.dll.so => 20
	i64 u0x66a4e5c6a3fb0bae, ; 406: lib_Xamarin.AndroidX.Lifecycle.ViewModel.Android.dll.so => 252
	i64 u0x66d13304ce1a3efa, ; 407: Xamarin.AndroidX.CursorAdapter => 230
	i64 u0x674303f65d8fad6f, ; 408: lib_System.Net.Quic.dll.so => 73
	i64 u0x6756ca4cad62e9d6, ; 409: lib_Xamarin.AndroidX.ConstraintLayout.Core.dll.so => 225
	i64 u0x67c0802770244408, ; 410: System.Windows.dll => 158
	i64 u0x68100b69286e27cd, ; 411: lib_System.Formats.Tar.dll.so => 39
	i64 u0x68558ec653afa616, ; 412: lib-da-Microsoft.Maui.Controls.resources.dll.so => 300
	i64 u0x6872ec7a2e36b1ac, ; 413: System.Drawing.Primitives.dll => 35
	i64 u0x68fbbbe2eb455198, ; 414: System.Formats.Asn1 => 38
	i64 u0x69063fc0ba8e6bdd, ; 415: he/Microsoft.Maui.Controls.resources.dll => 306
	i64 u0x6a4d7577b2317255, ; 416: System.Runtime.InteropServices.dll => 110
	i64 u0x6ace3b74b15ee4a4, ; 417: nb/Microsoft.Maui.Controls.resources => 315
	i64 u0x6afcedb171067e2b, ; 418: System.Core.dll => 21
	i64 u0x6bef98e124147c24, ; 419: Xamarin.Jetbrains.Annotations => 289
	i64 u0x6ce874bff138ce2b, ; 420: Xamarin.AndroidX.Lifecycle.ViewModel.dll => 251
	i64 u0x6d12bfaa99c72b1f, ; 421: lib_Microsoft.Maui.Graphics.dll.so => 204
	i64 u0x6d70755158ca866e, ; 422: lib_System.ComponentModel.EventBasedAsync.dll.so => 15
	i64 u0x6d79993361e10ef2, ; 423: Microsoft.Extensions.Primitives => 199
	i64 u0x6d7eeca99577fc8b, ; 424: lib_System.Net.WebProxy.dll.so => 81
	i64 u0x6d8515b19946b6a2, ; 425: System.Net.WebProxy.dll => 81
	i64 u0x6d86d56b84c8eb71, ; 426: lib_Xamarin.AndroidX.CursorAdapter.dll.so => 230
	i64 u0x6d9bea6b3e895cf7, ; 427: Microsoft.Extensions.Primitives.dll => 199
	i64 u0x6e25a02c3833319a, ; 428: lib_Xamarin.AndroidX.Navigation.Fragment.dll.so => 259
	i64 u0x6e79c6bd8627412a, ; 429: Xamarin.AndroidX.SavedState.SavedState.Ktx => 268
	i64 u0x6e838d9a2a6f6c9e, ; 430: lib_System.ValueTuple.dll.so => 155
	i64 u0x6e9965ce1095e60a, ; 431: lib_System.Core.dll.so => 21
	i64 u0x6fd2265da78b93a4, ; 432: lib_Microsoft.Maui.dll.so => 202
	i64 u0x6fdfc7de82c33008, ; 433: cs/Microsoft.Maui.Controls.resources => 299
	i64 u0x6ffc4967cc47ba57, ; 434: System.IO.FileSystem.Watcher.dll => 50
	i64 u0x701cd46a1c25a5fe, ; 435: System.IO.FileSystem.dll => 51
	i64 u0x70e99f48c05cb921, ; 436: tr/Microsoft.Maui.Controls.resources.dll => 325
	i64 u0x70fd3deda22442d2, ; 437: lib-nb-Microsoft.Maui.Controls.resources.dll.so => 315
	i64 u0x71485e7ffdb4b958, ; 438: System.Reflection.Extensions => 96
	i64 u0x7162a2fce67a945f, ; 439: lib_Xamarin.Android.Glide.Annotations.dll.so => 206
	i64 u0x717530326f808838, ; 440: lib_Microsoft.Extensions.Diagnostics.Abstractions.dll.so => 190
	i64 u0x71a495ea3761dde8, ; 441: lib-it-Microsoft.Maui.Controls.resources.dll.so => 311
	i64 u0x71ad672adbe48f35, ; 442: System.ComponentModel.Primitives.dll => 16
	i64 u0x720f102581a4a5c8, ; 443: Xamarin.AndroidX.Core.ViewTree => 229
	i64 u0x725f5a9e82a45c81, ; 444: System.Security.Cryptography.Encoding => 125
	i64 u0x72b1fb4109e08d7b, ; 445: lib-hr-Microsoft.Maui.Controls.resources.dll.so => 308
	i64 u0x72e0300099accce1, ; 446: System.Xml.XPath.XDocument => 163
	i64 u0x730bfb248998f67a, ; 447: System.IO.Compression.ZipFile => 45
	i64 u0x732b2d67b9e5c47b, ; 448: Xamarin.Google.ErrorProne.Annotations.dll => 287
	i64 u0x734b76fdc0dc05bb, ; 449: lib_GoogleGson.dll.so => 181
	i64 u0x73a6be34e822f9d1, ; 450: lib_System.Runtime.Serialization.dll.so => 118
	i64 u0x73e4ce94e2eb6ffc, ; 451: lib_System.Memory.dll.so => 64
	i64 u0x743a1eccf080489a, ; 452: WindowsBase.dll => 169
	i64 u0x755a91767330b3d4, ; 453: lib_Microsoft.Extensions.Configuration.dll.so => 182
	i64 u0x75c326eb821b85c4, ; 454: lib_System.ComponentModel.DataAnnotations.dll.so => 14
	i64 u0x76012e7334db86e5, ; 455: lib_Xamarin.AndroidX.SavedState.dll.so => 266
	i64 u0x76ca07b878f44da0, ; 456: System.Runtime.Numerics.dll => 113
	i64 u0x7736c8a96e51a061, ; 457: lib_Xamarin.AndroidX.Annotation.Jvm.dll.so => 213
	i64 u0x778a805e625329ef, ; 458: System.Linq.Parallel => 61
	i64 u0x77d9074d8f33a303, ; 459: lib_System.Net.ServerSentEvents.dll.so => 76
	i64 u0x77f8a4acc2fdc449, ; 460: System.Security.Cryptography.Cng.dll => 123
	i64 u0x780bc73597a503a9, ; 461: lib-ms-Microsoft.Maui.Controls.resources.dll.so => 314
	i64 u0x782c5d8eb99ff201, ; 462: lib_Microsoft.VisualBasic.Core.dll.so => 2
	i64 u0x783606d1e53e7a1a, ; 463: th/Microsoft.Maui.Controls.resources.dll => 324
	i64 u0x78a45e51311409b6, ; 464: Xamarin.AndroidX.Fragment.dll => 238
	i64 u0x78ed4ab8f9d800a1, ; 465: Xamarin.AndroidX.Lifecycle.ViewModel => 251
	i64 u0x7a5207a7c82d30b4, ; 466: lib_Xamarin.JSpecify.dll.so => 290
	i64 u0x7a7e7eddf79c5d26, ; 467: lib_Xamarin.AndroidX.Lifecycle.ViewModel.dll.so => 251
	i64 u0x7a9a57d43b0845fa, ; 468: System.AppContext => 6
	i64 u0x7ad0f4f1e5d08183, ; 469: Xamarin.AndroidX.Collection.dll => 220
	i64 u0x7adb8da2ac89b647, ; 470: fi/Microsoft.Maui.Controls.resources.dll => 304
	i64 u0x7b13d9eaa944ade8, ; 471: Xamarin.AndroidX.DynamicAnimation.dll => 234
	i64 u0x7bef86a4335c4870, ; 472: System.ComponentModel.TypeConverter => 17
	i64 u0x7c0820144cd34d6a, ; 473: sk/Microsoft.Maui.Controls.resources.dll => 322
	i64 u0x7c2a0bd1e0f988fc, ; 474: lib-de-Microsoft.Maui.Controls.resources.dll.so => 301
	i64 u0x7c41d387501568ba, ; 475: System.Net.WebClient.dll => 79
	i64 u0x7c482cd79bd24b13, ; 476: lib_Xamarin.AndroidX.ConstraintLayout.dll.so => 224
	i64 u0x7c60acf6404e96b6, ; 477: Xamarin.AndroidX.Navigation.Common.Android.dll => 258
	i64 u0x7cc637f941f716d0, ; 478: CommunityToolkit.Maui.Core => 179
	i64 u0x7cd2ec8eaf5241cd, ; 479: System.Security.dll => 133
	i64 u0x7cf9ae50dd350622, ; 480: Xamarin.Jetbrains.Annotations.dll => 289
	i64 u0x7d649b75d580bb42, ; 481: ms/Microsoft.Maui.Controls.resources.dll => 314
	i64 u0x7d8ee2bdc8e3aad1, ; 482: System.Numerics.Vectors => 85
	i64 u0x7df5df8db8eaa6ac, ; 483: Microsoft.Extensions.Logging.Debug => 197
	i64 u0x7dfc3d6d9d8d7b70, ; 484: System.Collections => 12
	i64 u0x7e2e564fa2f76c65, ; 485: lib_System.Diagnostics.Tracing.dll.so => 34
	i64 u0x7e302e110e1e1346, ; 486: lib_System.Security.Claims.dll.so => 121
	i64 u0x7e4465b3f78ad8d0, ; 487: Xamarin.KotlinX.Serialization.Core.dll => 295
	i64 u0x7e571cad5915e6c3, ; 488: lib_Xamarin.AndroidX.Lifecycle.Process.dll.so => 246
	i64 u0x7e6ac99e4e8df72f, ; 489: System.IO.Hashing => 176
	i64 u0x7e6b1ca712437d7d, ; 490: Xamarin.AndroidX.Emoji2.ViewsHelper => 236
	i64 u0x7e946809d6008ef2, ; 491: lib_System.ObjectModel.dll.so => 87
	i64 u0x7ea0272c1b4a9635, ; 492: lib_Xamarin.Android.Glide.dll.so => 205
	i64 u0x7ecc13347c8fd849, ; 493: lib_System.ComponentModel.dll.so => 18
	i64 u0x7f00ddd9b9ca5a13, ; 494: Xamarin.AndroidX.ViewPager.dll => 279
	i64 u0x7f9351cd44b1273f, ; 495: Microsoft.Extensions.Configuration.Abstractions => 183
	i64 u0x7fbd557c99b3ce6f, ; 496: lib_Xamarin.AndroidX.Lifecycle.LiveData.Core.dll.so => 244
	i64 u0x8076a9a44a2ca331, ; 497: System.Net.Quic => 73
	i64 u0x80da183a87731838, ; 498: System.Reflection.Metadata => 97
	i64 u0x812c069d5cdecc17, ; 499: System.dll => 168
	i64 u0x81381be520a60adb, ; 500: Xamarin.AndroidX.Interpolator.dll => 240
	i64 u0x81657cec2b31e8aa, ; 501: System.Net => 84
	i64 u0x81ab745f6c0f5ce6, ; 502: zh-Hant/Microsoft.Maui.Controls.resources => 330
	i64 u0x8277f2be6b5ce05f, ; 503: Xamarin.AndroidX.AppCompat => 214
	i64 u0x828f06563b30bc50, ; 504: lib_Xamarin.AndroidX.CardView.dll.so => 219
	i64 u0x82b399cb01b531c4, ; 505: lib_System.Web.dll.so => 157
	i64 u0x82df8f5532a10c59, ; 506: lib_System.Drawing.dll.so => 36
	i64 u0x82f0b6e911d13535, ; 507: lib_System.Transactions.dll.so => 154
	i64 u0x82f6403342e12049, ; 508: uk/Microsoft.Maui.Controls.resources => 326
	i64 u0x83c14ba66c8e2b8c, ; 509: zh-Hans/Microsoft.Maui.Controls.resources => 329
	i64 u0x8417d039fcc895af, ; 510: MissionPlanner.dll => 0
	i64 u0x846ce984efea52c7, ; 511: System.Threading.Tasks.Parallel.dll => 147
	i64 u0x84ae73148a4557d2, ; 512: lib_System.IO.Pipes.dll.so => 56
	i64 u0x84b01102c12a9232, ; 513: System.Runtime.Serialization.Json.dll => 115
	i64 u0x850c5ba0b57ce8e7, ; 514: lib_Xamarin.AndroidX.Collection.dll.so => 220
	i64 u0x851d02edd334b044, ; 515: Xamarin.AndroidX.VectorDrawable => 276
	i64 u0x85c919db62150978, ; 516: Xamarin.AndroidX.Transition.dll => 275
	i64 u0x8662aaeb94fef37f, ; 517: lib_System.Dynamic.Runtime.dll.so => 37
	i64 u0x86a909228dc7657b, ; 518: lib-zh-Hant-Microsoft.Maui.Controls.resources.dll.so => 330
	i64 u0x86b3e00c36b84509, ; 519: Microsoft.Extensions.Configuration.dll => 182
	i64 u0x86b62cb077ec4fd7, ; 520: System.Runtime.Serialization.Xml => 117
	i64 u0x8704193f462e892e, ; 521: lib_Microsoft.Extensions.FileSystemGlobbing.dll.so => 193
	i64 u0x8706ffb12bf3f53d, ; 522: Xamarin.AndroidX.Annotation.Experimental => 212
	i64 u0x872a5b14c18d328c, ; 523: System.ComponentModel.DataAnnotations => 14
	i64 u0x872fb9615bc2dff0, ; 524: Xamarin.Android.Glide.Annotations.dll => 206
	i64 u0x87c69b87d9283884, ; 525: lib_System.Threading.Thread.dll.so => 149
	i64 u0x87f6569b25707834, ; 526: System.IO.Compression.Brotli.dll => 43
	i64 u0x8842b3a5d2d3fb36, ; 527: Microsoft.Maui.Essentials => 203
	i64 u0x88926583efe7ee86, ; 528: Xamarin.AndroidX.Activity.Ktx.dll => 210
	i64 u0x88ba6bc4f7762b03, ; 529: lib_System.Reflection.dll.so => 100
	i64 u0x88bda98e0cffb7a9, ; 530: lib_Xamarin.KotlinX.Coroutines.Core.Jvm.dll.so => 294
	i64 u0x8930322c7bd8f768, ; 531: netstandard => 171
	i64 u0x897a606c9e39c75f, ; 532: lib_System.ComponentModel.Primitives.dll.so => 16
	i64 u0x898a5c6bc9e47ec1, ; 533: lib_Xamarin.AndroidX.SavedState.SavedState.Android.dll.so => 267
	i64 u0x89911a22005b92b7, ; 534: System.IO.FileSystem.DriveInfo.dll => 48
	i64 u0x89c5188089ec2cd5, ; 535: lib_System.Runtime.InteropServices.RuntimeInformation.dll.so => 109
	i64 u0x8a19e3dc71b34b2c, ; 536: System.Reflection.TypeExtensions.dll => 99
	i64 u0x8ad229ea26432ee2, ; 537: Xamarin.AndroidX.Loader => 256
	i64 u0x8b4ff5d0fdd5faa1, ; 538: lib_System.Diagnostics.DiagnosticSource.dll.so => 27
	i64 u0x8b541d476eb3774c, ; 539: System.Security.Principal.Windows => 130
	i64 u0x8b8d01333a96d0b5, ; 540: System.Diagnostics.Process.dll => 29
	i64 u0x8b9ceca7acae3451, ; 541: lib-he-Microsoft.Maui.Controls.resources.dll.so => 306
	i64 u0x8c575135aa1ccef4, ; 542: Microsoft.Extensions.FileProviders.Abstractions => 191
	i64 u0x8cb8f612b633affb, ; 543: Xamarin.AndroidX.SavedState.SavedState.Ktx.dll => 268
	i64 u0x8cdfdb4ce85fb925, ; 544: lib_System.Security.Principal.Windows.dll.so => 130
	i64 u0x8cdfe7b8f4caa426, ; 545: System.IO.Compression.FileSystem => 44
	i64 u0x8d0f420977c2c1c7, ; 546: Xamarin.AndroidX.CursorAdapter.dll => 230
	i64 u0x8d52f7ea2796c531, ; 547: Xamarin.AndroidX.Emoji2.dll => 235
	i64 u0x8d7b8ab4b3310ead, ; 548: System.Threading => 152
	i64 u0x8da188285aadfe8e, ; 549: System.Collections.Concurrent => 8
	i64 u0x8e8f269ad1e1ff94, ; 550: lib_Xamarin.AndroidX.Tracing.Tracing.Android.dll.so => 274
	i64 u0x8ed807bfe9858dfc, ; 551: Xamarin.AndroidX.Navigation.Common => 257
	i64 u0x8ee08b8194a30f48, ; 552: lib-hi-Microsoft.Maui.Controls.resources.dll.so => 307
	i64 u0x8ef7601039857a44, ; 553: lib-ro-Microsoft.Maui.Controls.resources.dll.so => 320
	i64 u0x8f32c6f611f6ffab, ; 554: pt/Microsoft.Maui.Controls.resources.dll => 319
	i64 u0x8f44b45eb046bbd1, ; 555: System.ServiceModel.Web.dll => 134
	i64 u0x8f8829d21c8985a4, ; 556: lib-pt-BR-Microsoft.Maui.Controls.resources.dll.so => 318
	i64 u0x8fbf5b0114c6dcef, ; 557: System.Globalization.dll => 42
	i64 u0x8fcc8c2a81f3d9e7, ; 558: Xamarin.KotlinX.Serialization.Core => 295
	i64 u0x90263f8448b8f572, ; 559: lib_System.Diagnostics.TraceSource.dll.so => 33
	i64 u0x903101b46fb73a04, ; 560: _Microsoft.Android.Resource.Designer => 331
	i64 u0x90393bd4865292f3, ; 561: lib_System.IO.Compression.dll.so => 46
	i64 u0x905e2b8e7ae91ae6, ; 562: System.Threading.Tasks.Extensions.dll => 146
	i64 u0x90634f86c5ebe2b5, ; 563: Xamarin.AndroidX.Lifecycle.ViewModel.Android => 252
	i64 u0x907b636704ad79ef, ; 564: lib_Microsoft.Maui.Controls.Xaml.dll.so => 201
	i64 u0x90e9efbfd68593e0, ; 565: lib_Xamarin.AndroidX.Lifecycle.LiveData.dll.so => 243
	i64 u0x91418dc638b29e68, ; 566: lib_Xamarin.AndroidX.CustomView.dll.so => 231
	i64 u0x9157bd523cd7ed36, ; 567: lib_System.Text.Json.dll.so => 140
	i64 u0x91a74f07b30d37e2, ; 568: System.Linq.dll => 63
	i64 u0x91cb86ea3b17111d, ; 569: System.ServiceModel.Web => 134
	i64 u0x91fa41a87223399f, ; 570: ca/Microsoft.Maui.Controls.resources.dll => 298
	i64 u0x92054e486c0c7ea7, ; 571: System.IO.FileSystem.DriveInfo => 48
	i64 u0x928614058c40c4cd, ; 572: lib_System.Xml.XPath.XDocument.dll.so => 163
	i64 u0x92b138fffca2b01e, ; 573: lib_Xamarin.AndroidX.Arch.Core.Runtime.dll.so => 217
	i64 u0x92dfc2bfc6c6a888, ; 574: Xamarin.AndroidX.Lifecycle.LiveData => 243
	i64 u0x933da2c779423d68, ; 575: Xamarin.Android.Glide.Annotations => 206
	i64 u0x9388aad9b7ae40ce, ; 576: lib_Xamarin.AndroidX.Lifecycle.Common.dll.so => 241
	i64 u0x93cfa73ab28d6e35, ; 577: ms/Microsoft.Maui.Controls.resources => 314
	i64 u0x941c00d21e5c0679, ; 578: lib_Xamarin.AndroidX.Transition.dll.so => 275
	i64 u0x944077d8ca3c6580, ; 579: System.IO.Compression.dll => 46
	i64 u0x948cffedc8ed7960, ; 580: System.Xml => 167
	i64 u0x94bbeab0d4764588, ; 581: System.IO.Hashing.dll => 176
	i64 u0x94c8990839c4bdb1, ; 582: lib_Xamarin.AndroidX.Interpolator.dll.so => 240
	i64 u0x967fc325e09bfa8c, ; 583: es/Microsoft.Maui.Controls.resources => 303
	i64 u0x9686161486d34b81, ; 584: lib_Xamarin.AndroidX.ExifInterface.dll.so => 237
	i64 u0x9732d8dbddea3d9a, ; 585: id/Microsoft.Maui.Controls.resources => 310
	i64 u0x978be80e5210d31b, ; 586: Microsoft.Maui.Graphics.dll => 204
	i64 u0x97b8c771ea3e4220, ; 587: System.ComponentModel.dll => 18
	i64 u0x97e144c9d3c6976e, ; 588: System.Collections.Concurrent.dll => 8
	i64 u0x984184e3c70d4419, ; 589: GoogleGson => 181
	i64 u0x9843944103683dd3, ; 590: Xamarin.AndroidX.Core.Core.Ktx => 228
	i64 u0x98b05cc81e6f333c, ; 591: Xamarin.AndroidX.SavedState.SavedState.Android.dll => 267
	i64 u0x98d720cc4597562c, ; 592: System.Security.Cryptography.OpenSsl => 126
	i64 u0x991d510397f92d9d, ; 593: System.Linq.Expressions => 60
	i64 u0x996ceeb8a3da3d67, ; 594: System.Threading.Overlapped.dll => 144
	i64 u0x999cb19e1a04ffd3, ; 595: CommunityToolkit.Mvvm.dll => 180
	i64 u0x99a00ca5270c6878, ; 596: Xamarin.AndroidX.Navigation.Runtime => 260
	i64 u0x99cdc6d1f2d3a72f, ; 597: ko/Microsoft.Maui.Controls.resources.dll => 313
	i64 u0x9a01b1da98b6ee10, ; 598: Xamarin.AndroidX.Lifecycle.Runtime.dll => 247
	i64 u0x9a5ccc274fd6e6ee, ; 599: Jsr305Binding.dll => 285
	i64 u0x9ae6940b11c02876, ; 600: lib_Xamarin.AndroidX.Window.dll.so => 281
	i64 u0x9b211a749105beac, ; 601: System.Transactions.Local => 153
	i64 u0x9b8734714671022d, ; 602: System.Threading.Tasks.Dataflow.dll => 145
	i64 u0x9bc6aea27fbf034f, ; 603: lib_Xamarin.KotlinX.Coroutines.Core.dll.so => 293
	i64 u0x9c244ac7cda32d26, ; 604: System.Security.Cryptography.X509Certificates.dll => 128
	i64 u0x9c465f280cf43733, ; 605: lib_Xamarin.KotlinX.Coroutines.Android.dll.so => 292
	i64 u0x9c8f6872beab6408, ; 606: System.Xml.XPath.XDocument.dll => 163
	i64 u0x9ce01cf91101ae23, ; 607: System.Xml.XmlDocument => 165
	i64 u0x9d128180c81d7ce6, ; 608: Xamarin.AndroidX.CustomView.PoolingContainer => 232
	i64 u0x9d5dbcf5a48583fe, ; 609: lib_Xamarin.AndroidX.Activity.dll.so => 209
	i64 u0x9d74dee1a7725f34, ; 610: Microsoft.Extensions.Configuration.Abstractions.dll => 183
	i64 u0x9dd0e195825d65c6, ; 611: lib_Xamarin.AndroidX.Navigation.Runtime.Android.dll.so => 261
	i64 u0x9e4534b6adaf6e84, ; 612: nl/Microsoft.Maui.Controls.resources => 316
	i64 u0x9e4b95dec42769f7, ; 613: System.Diagnostics.Debug.dll => 26
	i64 u0x9eaf1efdf6f7267e, ; 614: Xamarin.AndroidX.Navigation.Common.dll => 257
	i64 u0x9ef542cf1f78c506, ; 615: Xamarin.AndroidX.Lifecycle.LiveData.Core => 244
	i64 u0x9fbb2961ca18e5c2, ; 616: Microsoft.Extensions.FileProviders.Physical.dll => 192
	i64 u0xa00832eb975f56a8, ; 617: lib_System.Net.dll.so => 84
	i64 u0xa0ad78236b7b267f, ; 618: Xamarin.AndroidX.Window => 281
	i64 u0xa0d8259f4cc284ec, ; 619: lib_System.Security.Cryptography.dll.so => 129
	i64 u0xa0e17ca50c77a225, ; 620: lib_Xamarin.Google.Crypto.Tink.Android.dll.so => 286
	i64 u0xa0ff9b3e34d92f11, ; 621: lib_System.Resources.Writer.dll.so => 103
	i64 u0xa12fbfb4da97d9f3, ; 622: System.Threading.Timer.dll => 151
	i64 u0xa1440773ee9d341e, ; 623: Xamarin.Google.Android.Material => 284
	i64 u0xa18c39c44cdc3465, ; 624: Xamarin.AndroidX.Window.WindowCore => 282
	i64 u0xa1b9d7c27f47219f, ; 625: Xamarin.AndroidX.Navigation.UI.dll => 262
	i64 u0xa2572680829d2c7c, ; 626: System.IO.Pipelines.dll => 54
	i64 u0xa26597e57ee9c7f6, ; 627: System.Xml.XmlDocument.dll => 165
	i64 u0xa308401900e5bed3, ; 628: lib_mscorlib.dll.so => 170
	i64 u0xa395572e7da6c99d, ; 629: lib_System.Security.dll.so => 133
	i64 u0xa3e683f24b43af6f, ; 630: System.Dynamic.Runtime.dll => 37
	i64 u0xa4145becdee3dc4f, ; 631: Xamarin.AndroidX.VectorDrawable.Animated => 277
	i64 u0xa46aa1eaa214539b, ; 632: ko/Microsoft.Maui.Controls.resources => 313
	i64 u0xa4d20d2ff0563d26, ; 633: lib_CommunityToolkit.Mvvm.dll.so => 180
	i64 u0xa4edc8f2ceae241a, ; 634: System.Data.Common.dll => 22
	i64 u0xa5494f40f128ce6a, ; 635: System.Runtime.Serialization.Formatters.dll => 114
	i64 u0xa54b74df83dce92b, ; 636: System.Reflection.DispatchProxy => 92
	i64 u0xa5b7152421ed6d98, ; 637: lib_System.IO.FileSystem.Watcher.dll.so => 50
	i64 u0xa5c3844f17b822db, ; 638: lib_System.Linq.Parallel.dll.so => 61
	i64 u0xa5ce5c755bde8cb8, ; 639: lib_System.Security.Cryptography.Csp.dll.so => 124
	i64 u0xa5e599d1e0524750, ; 640: System.Numerics.Vectors.dll => 85
	i64 u0xa5f1ba49b85dd355, ; 641: System.Security.Cryptography.dll => 129
	i64 u0xa61975a5a37873ea, ; 642: lib_System.Xml.XmlSerializer.dll.so => 166
	i64 u0xa6593e21584384d2, ; 643: lib_Jsr305Binding.dll.so => 285
	i64 u0xa66cbee0130865f7, ; 644: lib_WindowsBase.dll.so => 169
	i64 u0xa67dbee13e1df9ca, ; 645: Xamarin.AndroidX.SavedState.dll => 266
	i64 u0xa684b098dd27b296, ; 646: lib_Xamarin.AndroidX.Security.SecurityCrypto.dll.so => 269
	i64 u0xa68a420042bb9b1f, ; 647: Xamarin.AndroidX.DrawerLayout.dll => 233
	i64 u0xa6d26156d1cacc7c, ; 648: Xamarin.Android.Glide.dll => 205
	i64 u0xa75386b5cb9595aa, ; 649: Xamarin.AndroidX.Lifecycle.Runtime.Android => 248
	i64 u0xa763fbb98df8d9fb, ; 650: lib_Microsoft.Win32.Primitives.dll.so => 4
	i64 u0xa78ce3745383236a, ; 651: Xamarin.AndroidX.Lifecycle.Common.Jvm => 242
	i64 u0xa7c31b56b4dc7b33, ; 652: hu/Microsoft.Maui.Controls.resources => 309
	i64 u0xa7eab29ed44b4e7a, ; 653: Mono.Android.Export => 173
	i64 u0xa8195217cbf017b7, ; 654: Microsoft.VisualBasic.Core => 2
	i64 u0xa82fd211eef00a5b, ; 655: Microsoft.Extensions.FileProviders.Physical => 192
	i64 u0xa859a95830f367ff, ; 656: lib_Xamarin.AndroidX.Lifecycle.ViewModel.Ktx.dll.so => 253
	i64 u0xa8b52f21e0dbe690, ; 657: System.Runtime.Serialization.dll => 118
	i64 u0xa8ee4ed7de2efaee, ; 658: Xamarin.AndroidX.Annotation.dll => 211
	i64 u0xa95590e7c57438a4, ; 659: System.Configuration => 19
	i64 u0xa964304b5631e28a, ; 660: CommunityToolkit.Maui.Core.dll => 179
	i64 u0xaa2219c8e3449ff5, ; 661: Microsoft.Extensions.Logging.Abstractions => 196
	i64 u0xaa443ac34067eeef, ; 662: System.Private.Xml.dll => 91
	i64 u0xaa52de307ef5d1dd, ; 663: System.Net.Http => 66
	i64 u0xaa9a7b0214a5cc5c, ; 664: System.Diagnostics.StackTrace.dll => 30
	i64 u0xaaaf86367285a918, ; 665: Microsoft.Extensions.DependencyInjection.Abstractions.dll => 189
	i64 u0xaaf84bb3f052a265, ; 666: el/Microsoft.Maui.Controls.resources => 302
	i64 u0xab38940ee4c634df, ; 667: MissionPlanner => 0
	i64 u0xab3fb0736c091056, ; 668: lib_MissionPlanner.dll.so => 0
	i64 u0xab9af77b5b67a0b8, ; 669: Xamarin.AndroidX.ConstraintLayout.Core => 225
	i64 u0xab9c1b2687d86b0b, ; 670: lib_System.Linq.Expressions.dll.so => 60
	i64 u0xac2af3fa195a15ce, ; 671: System.Runtime.Numerics => 113
	i64 u0xac5376a2a538dc10, ; 672: Xamarin.AndroidX.Lifecycle.LiveData.Core.dll => 244
	i64 u0xac5acae88f60357e, ; 673: System.Diagnostics.Tools.dll => 32
	i64 u0xac79c7e46047ad98, ; 674: System.Security.Principal.Windows.dll => 130
	i64 u0xac98d31068e24591, ; 675: System.Xml.XDocument => 162
	i64 u0xacd46e002c3ccb97, ; 676: ro/Microsoft.Maui.Controls.resources => 320
	i64 u0xacdd9e4180d56dda, ; 677: Xamarin.AndroidX.Concurrent.Futures => 223
	i64 u0xacf42eea7ef9cd12, ; 678: System.Threading.Channels => 143
	i64 u0xad89c07347f1bad6, ; 679: nl/Microsoft.Maui.Controls.resources.dll => 316
	i64 u0xadbb53caf78a79d2, ; 680: System.Web.HttpUtility => 156
	i64 u0xadc90ab061a9e6e4, ; 681: System.ComponentModel.TypeConverter.dll => 17
	i64 u0xadca1b9030b9317e, ; 682: Xamarin.AndroidX.Collection.Ktx => 222
	i64 u0xadd8eda2edf396ad, ; 683: Xamarin.Android.Glide.GifDecoder => 208
	i64 u0xadf4cf30debbeb9a, ; 684: System.Net.ServicePoint.dll => 77
	i64 u0xadf511667bef3595, ; 685: System.Net.Security => 75
	i64 u0xae0aaa94fdcfce0f, ; 686: System.ComponentModel.EventBasedAsync.dll => 15
	i64 u0xae282bcd03739de7, ; 687: Java.Interop => 172
	i64 u0xae53579c90db1107, ; 688: System.ObjectModel.dll => 87
	i64 u0xaf732d0b2193b8f5, ; 689: System.Security.Cryptography.OpenSsl.dll => 126
	i64 u0xafdb94dbccd9d11c, ; 690: Xamarin.AndroidX.Lifecycle.LiveData.dll => 243
	i64 u0xafe29f45095518e7, ; 691: lib_Xamarin.AndroidX.Lifecycle.ViewModelSavedState.dll.so => 254
	i64 u0xafe8e4d1ae2a2c43, ; 692: lib_Microsoft.Extensions.Configuration.EnvironmentVariables.dll.so => 186
	i64 u0xb03ae931fb25607e, ; 693: Xamarin.AndroidX.ConstraintLayout => 224
	i64 u0xb05cc42cd94c6d9d, ; 694: lib-sv-Microsoft.Maui.Controls.resources.dll.so => 323
	i64 u0xb0ac21bec8f428c5, ; 695: Xamarin.AndroidX.Lifecycle.Runtime.Ktx.Android.dll => 250
	i64 u0xb0bb43dc52ea59f9, ; 696: System.Diagnostics.Tracing.dll => 34
	i64 u0xb1dd05401aa8ee63, ; 697: System.Security.AccessControl => 120
	i64 u0xb220631954820169, ; 698: System.Text.RegularExpressions => 141
	i64 u0xb2376e1dbf8b4ed7, ; 699: System.Security.Cryptography.Csp => 124
	i64 u0xb2a1959fe95c5402, ; 700: lib_System.Runtime.InteropServices.JavaScript.dll.so => 108
	i64 u0xb2a3f67f3bf29fce, ; 701: da/Microsoft.Maui.Controls.resources => 300
	i64 u0xb3874072ee0ecf8c, ; 702: Xamarin.AndroidX.VectorDrawable.Animated.dll => 277
	i64 u0xb3f0a0fcda8d3ebc, ; 703: Xamarin.AndroidX.CardView => 219
	i64 u0xb46be1aa6d4fff93, ; 704: hi/Microsoft.Maui.Controls.resources => 307
	i64 u0xb477491be13109d8, ; 705: ar/Microsoft.Maui.Controls.resources => 297
	i64 u0xb4bd7015ecee9d86, ; 706: System.IO.Pipelines => 54
	i64 u0xb4c53d9749c5f226, ; 707: lib_System.IO.FileSystem.AccessControl.dll.so => 47
	i64 u0xb4ff710863453fda, ; 708: System.Diagnostics.FileVersionInfo.dll => 28
	i64 u0xb54092076b15e062, ; 709: System.Threading.AccessControl => 142
	i64 u0xb5c38bf497a4cfe2, ; 710: lib_System.Threading.Tasks.dll.so => 148
	i64 u0xb5c7fcdafbc67ee4, ; 711: Microsoft.Extensions.Logging.Abstractions.dll => 196
	i64 u0xb5ea31d5244c6626, ; 712: System.Threading.ThreadPool.dll => 150
	i64 u0xb7212c4683a94afe, ; 713: System.Drawing.Primitives => 35
	i64 u0xb7b7753d1f319409, ; 714: sv/Microsoft.Maui.Controls.resources => 323
	i64 u0xb81a2c6e0aee50fe, ; 715: lib_System.Private.CoreLib.dll.so => 177
	i64 u0xb8c60af47c08d4da, ; 716: System.Net.ServicePoint => 77
	i64 u0xb8e68d20aad91196, ; 717: lib_System.Xml.XPath.dll.so => 164
	i64 u0xb9185c33a1643eed, ; 718: Microsoft.CSharp.dll => 1
	i64 u0xb960d6b2200ba320, ; 719: Xamarin.AndroidX.Lifecycle.ViewModelSavedState.Android.dll => 255
	i64 u0xb9b8001adf4ed7cc, ; 720: lib_Xamarin.AndroidX.SlidingPaneLayout.dll.so => 270
	i64 u0xb9f64d3b230def68, ; 721: lib-pt-Microsoft.Maui.Controls.resources.dll.so => 319
	i64 u0xb9fc3c8a556e3691, ; 722: ja/Microsoft.Maui.Controls.resources => 312
	i64 u0xba4670aa94a2b3c6, ; 723: lib_System.Xml.XDocument.dll.so => 162
	i64 u0xba48785529705af9, ; 724: System.Collections.dll => 12
	i64 u0xba965b8c86359996, ; 725: lib_System.Windows.dll.so => 158
	i64 u0xbb272a43608ae0e8, ; 726: Microsoft.Extensions.Configuration.EnvironmentVariables => 186
	i64 u0xbb286883bc35db36, ; 727: System.Transactions.dll => 154
	i64 u0xbb65706fde942ce3, ; 728: System.Net.Sockets => 78
	i64 u0xbba28979413cad9e, ; 729: lib_System.Runtime.CompilerServices.VisualC.dll.so => 105
	i64 u0xbbd180354b67271a, ; 730: System.Runtime.Serialization.Formatters => 114
	i64 u0xbc260cdba33291a3, ; 731: Xamarin.AndroidX.Arch.Core.Common.dll => 216
	i64 u0xbd0e2c0d55246576, ; 732: System.Net.Http.dll => 66
	i64 u0xbd3fbd85b9e1cb29, ; 733: lib_System.Net.HttpListener.dll.so => 67
	i64 u0xbd437a2cdb333d0d, ; 734: Xamarin.AndroidX.ViewPager2 => 280
	i64 u0xbd4f572d2bd0a789, ; 735: System.IO.Compression.ZipFile.dll => 45
	i64 u0xbd5d0b88d3d647a5, ; 736: lib_Xamarin.AndroidX.Browser.dll.so => 218
	i64 u0xbd877b14d0b56392, ; 737: System.Runtime.Intrinsics.dll => 111
	i64 u0xbe65a49036345cf4, ; 738: lib_System.Buffers.dll.so => 7
	i64 u0xbee38d4a88835966, ; 739: Xamarin.AndroidX.AppCompat.AppCompatResources => 215
	i64 u0xbef9919db45b4ca7, ; 740: System.IO.Pipes.AccessControl => 55
	i64 u0xbf0fa68611139208, ; 741: lib_Xamarin.AndroidX.Annotation.dll.so => 211
	i64 u0xbfc1e1fb3095f2b3, ; 742: lib_System.Net.Http.Json.dll.so => 65
	i64 u0xc040a4ab55817f58, ; 743: ar/Microsoft.Maui.Controls.resources.dll => 297
	i64 u0xc07cadab29efeba0, ; 744: Xamarin.AndroidX.Core.Core.Ktx.dll => 228
	i64 u0xc0d928351ab5ca77, ; 745: System.Console.dll => 20
	i64 u0xc0f5a221a9383aea, ; 746: System.Runtime.Intrinsics => 111
	i64 u0xc111030af54d7191, ; 747: System.Resources.Writer => 103
	i64 u0xc12b8b3afa48329c, ; 748: lib_System.Linq.dll.so => 63
	i64 u0xc183ca0b74453aa9, ; 749: lib_System.Threading.Tasks.Dataflow.dll.so => 145
	i64 u0xc1ff9ae3cdb6e1e6, ; 750: Xamarin.AndroidX.Activity.dll => 209
	i64 u0xc26c064effb1dea9, ; 751: System.Buffers.dll => 7
	i64 u0xc27a0dbe1ae51104, ; 752: Microsoft.Extensions.Configuration.EnvironmentVariables.dll => 186
	i64 u0xc28c50f32f81cc73, ; 753: ja/Microsoft.Maui.Controls.resources.dll => 312
	i64 u0xc2902f6cf5452577, ; 754: lib_Mono.Android.Export.dll.so => 173
	i64 u0xc2a3bca55b573141, ; 755: System.IO.FileSystem.Watcher => 50
	i64 u0xc2bcfec99f69365e, ; 756: Xamarin.AndroidX.ViewPager2.dll => 280
	i64 u0xc30b52815b58ac2c, ; 757: lib_System.Runtime.Serialization.Xml.dll.so => 117
	i64 u0xc36d7d89c652f455, ; 758: System.Threading.Overlapped => 144
	i64 u0xc396b285e59e5493, ; 759: GoogleGson.dll => 181
	i64 u0xc3c86c1e5e12f03d, ; 760: WindowsBase => 169
	i64 u0xc421b61fd853169d, ; 761: lib_System.Net.WebSockets.Client.dll.so => 82
	i64 u0xc463e077917aa21d, ; 762: System.Runtime.Serialization.Json => 115
	i64 u0xc4d3858ed4d08512, ; 763: Xamarin.AndroidX.Lifecycle.ViewModelSavedState.dll => 254
	i64 u0xc50fded0ded1418c, ; 764: lib_System.ComponentModel.TypeConverter.dll.so => 17
	i64 u0xc519125d6bc8fb11, ; 765: lib_System.Net.Requests.dll.so => 74
	i64 u0xc5293b19e4dc230e, ; 766: Xamarin.AndroidX.Navigation.Fragment => 259
	i64 u0xc5325b2fcb37446f, ; 767: lib_System.Private.Xml.dll.so => 91
	i64 u0xc535cb9a21385d9b, ; 768: lib_Xamarin.Android.Glide.DiskLruCache.dll.so => 207
	i64 u0xc5a0f4b95a699af7, ; 769: lib_System.Private.Uri.dll.so => 89
	i64 u0xc5cdcd5b6277579e, ; 770: lib_System.Security.Cryptography.Algorithms.dll.so => 122
	i64 u0xc5ec286825cb0bf4, ; 771: Xamarin.AndroidX.Tracing.Tracing => 273
	i64 u0xc659b586d4c229e2, ; 772: Microsoft.Extensions.Configuration.FileExtensions.dll => 187
	i64 u0xc6706bc8aa7fe265, ; 773: Xamarin.AndroidX.Annotation.Jvm => 213
	i64 u0xc74d70d4aa96cef3, ; 774: Xamarin.AndroidX.Navigation.Runtime.Android => 261
	i64 u0xc7c01e7d7c93a110, ; 775: System.Text.Encoding.Extensions.dll => 137
	i64 u0xc7ce851898a4548e, ; 776: lib_System.Web.HttpUtility.dll.so => 156
	i64 u0xc809d4089d2556b2, ; 777: System.Runtime.InteropServices.JavaScript.dll => 108
	i64 u0xc858a28d9ee5a6c5, ; 778: lib_System.Collections.Specialized.dll.so => 11
	i64 u0xc8ac7c6bf1c2ec51, ; 779: System.Reflection.DispatchProxy.dll => 92
	i64 u0xc9c62c8f354ac568, ; 780: lib_System.Diagnostics.TextWriterTraceListener.dll.so => 31
	i64 u0xc9e54b32fc19baf3, ; 781: lib_CommunityToolkit.Maui.dll.so => 178
	i64 u0xca3a723e7342c5b6, ; 782: lib-tr-Microsoft.Maui.Controls.resources.dll.so => 325
	i64 u0xca5801070d9fccfb, ; 783: System.Text.Encoding => 138
	i64 u0xcab3493c70141c2d, ; 784: pl/Microsoft.Maui.Controls.resources => 317
	i64 u0xcacfddc9f7c6de76, ; 785: ro/Microsoft.Maui.Controls.resources.dll => 320
	i64 u0xcadbc92899a777f0, ; 786: Xamarin.AndroidX.Startup.StartupRuntime => 271
	i64 u0xcba1cb79f45292b5, ; 787: Xamarin.Android.Glide.GifDecoder.dll => 208
	i64 u0xcbb5f80c7293e696, ; 788: lib_System.Globalization.Calendars.dll.so => 40
	i64 u0xcbd4fdd9cef4a294, ; 789: lib__Microsoft.Android.Resource.Designer.dll.so => 331
	i64 u0xcc15da1e07bbd994, ; 790: Xamarin.AndroidX.SlidingPaneLayout => 270
	i64 u0xcc2876b32ef2794c, ; 791: lib_System.Text.RegularExpressions.dll.so => 141
	i64 u0xcc5c3bb714c4561e, ; 792: Xamarin.KotlinX.Coroutines.Core.Jvm.dll => 294
	i64 u0xcc76886e09b88260, ; 793: Xamarin.KotlinX.Serialization.Core.Jvm.dll => 296
	i64 u0xcc9fa2923aa1c9ef, ; 794: System.Diagnostics.Contracts.dll => 25
	i64 u0xccae9bb73e2326bd, ; 795: lib_System.IO.Hashing.dll.so => 176
	i64 u0xccf25c4b634ccd3a, ; 796: zh-Hans/Microsoft.Maui.Controls.resources.dll => 329
	i64 u0xcd10a42808629144, ; 797: System.Net.Requests => 74
	i64 u0xcdca1b920e9f53ba, ; 798: Xamarin.AndroidX.Interpolator => 240
	i64 u0xcdd0c48b6937b21c, ; 799: Xamarin.AndroidX.SwipeRefreshLayout => 272
	i64 u0xcf23d8093f3ceadf, ; 800: System.Diagnostics.DiagnosticSource.dll => 27
	i64 u0xcf5ff6b6b2c4c382, ; 801: System.Net.Mail.dll => 68
	i64 u0xcf8fc898f98b0d34, ; 802: System.Private.Xml.Linq => 90
	i64 u0xd04b5f59ed596e31, ; 803: System.Reflection.Metadata.dll => 97
	i64 u0xd063299fcfc0c93f, ; 804: lib_System.Runtime.Serialization.Json.dll.so => 115
	i64 u0xd0de8a113e976700, ; 805: System.Diagnostics.TextWriterTraceListener => 31
	i64 u0xd0fc33d5ae5d4cb8, ; 806: System.Runtime.Extensions => 106
	i64 u0xd1194e1d8a8de83c, ; 807: lib_Xamarin.AndroidX.Lifecycle.Common.Jvm.dll.so => 242
	i64 u0xd12beacdfc14f696, ; 808: System.Dynamic.Runtime => 37
	i64 u0xd16fd7fb9bbcd43e, ; 809: Microsoft.Extensions.Diagnostics.Abstractions => 190
	i64 u0xd198e7ce1b6a8344, ; 810: System.Net.Quic.dll => 73
	i64 u0xd3144156a3727ebe, ; 811: Xamarin.Google.Guava.ListenableFuture => 288
	i64 u0xd333d0af9e423810, ; 812: System.Runtime.InteropServices => 110
	i64 u0xd33a415cb4278969, ; 813: System.Security.Cryptography.Encoding.dll => 125
	i64 u0xd3426d966bb704f5, ; 814: Xamarin.AndroidX.AppCompat.AppCompatResources.dll => 215
	i64 u0xd3651b6fc3125825, ; 815: System.Private.Uri.dll => 89
	i64 u0xd373685349b1fe8b, ; 816: Microsoft.Extensions.Logging.dll => 195
	i64 u0xd3801faafafb7698, ; 817: System.Private.DataContractSerialization.dll => 88
	i64 u0xd3e4c8d6a2d5d470, ; 818: it/Microsoft.Maui.Controls.resources => 311
	i64 u0xd3edcc1f25459a50, ; 819: System.Reflection.Emit => 95
	i64 u0xd4645626dffec99d, ; 820: lib_Microsoft.Extensions.DependencyInjection.Abstractions.dll.so => 189
	i64 u0xd4fa0abb79079ea9, ; 821: System.Security.Principal.dll => 131
	i64 u0xd5507e11a2b2839f, ; 822: Xamarin.AndroidX.Lifecycle.ViewModelSavedState => 254
	i64 u0xd5d04bef8478ea19, ; 823: Xamarin.AndroidX.Tracing.Tracing.dll => 273
	i64 u0xd60815f26a12e140, ; 824: Microsoft.Extensions.Logging.Debug.dll => 197
	i64 u0xd6694f8359737e4e, ; 825: Xamarin.AndroidX.SavedState => 266
	i64 u0xd6949e129339eae5, ; 826: lib_Xamarin.AndroidX.Core.Core.Ktx.dll.so => 228
	i64 u0xd6d21782156bc35b, ; 827: Xamarin.AndroidX.SwipeRefreshLayout.dll => 272
	i64 u0xd6de019f6af72435, ; 828: Xamarin.AndroidX.ConstraintLayout.Core.dll => 225
	i64 u0xd70956d1e6deefb9, ; 829: Jsr305Binding => 285
	i64 u0xd72329819cbbbc44, ; 830: lib_Microsoft.Extensions.Configuration.Abstractions.dll.so => 183
	i64 u0xd72c760af136e863, ; 831: System.Xml.XmlSerializer.dll => 166
	i64 u0xd753f071e44c2a03, ; 832: lib_System.Security.SecureString.dll.so => 132
	i64 u0xd7b3764ada9d341d, ; 833: lib_Microsoft.Extensions.Logging.Abstractions.dll.so => 196
	i64 u0xd7f0088bc5ad71f2, ; 834: Xamarin.AndroidX.VersionedParcelable => 278
	i64 u0xd824ef6ab33f8f7a, ; 835: Xamarin.AndroidX.Window.WindowCore.dll => 282
	i64 u0xd8fb25e28ae30a12, ; 836: Xamarin.AndroidX.ProfileInstaller.ProfileInstaller.dll => 263
	i64 u0xda1dfa4c534a9251, ; 837: Microsoft.Extensions.DependencyInjection => 188
	i64 u0xdad05a11827959a3, ; 838: System.Collections.NonGeneric.dll => 10
	i64 u0xdaefdfe71aa53cf9, ; 839: System.IO.FileSystem.Primitives => 49
	i64 u0xdb5383ab5865c007, ; 840: lib-vi-Microsoft.Maui.Controls.resources.dll.so => 327
	i64 u0xdb58816721c02a59, ; 841: lib_System.Reflection.Emit.ILGeneration.dll.so => 93
	i64 u0xdbeda89f832aa805, ; 842: vi/Microsoft.Maui.Controls.resources.dll => 327
	i64 u0xdbf2a779fbc3ac31, ; 843: System.Transactions.Local.dll => 153
	i64 u0xdbf9607a441b4505, ; 844: System.Linq => 63
	i64 u0xdbfc90157a0de9b0, ; 845: lib_System.Text.Encoding.dll.so => 138
	i64 u0xdc75032002d1a212, ; 846: lib_System.Transactions.Local.dll.so => 153
	i64 u0xdca8be7403f92d4f, ; 847: lib_System.Linq.Queryable.dll.so => 62
	i64 u0xdce2c53525640bf3, ; 848: Microsoft.Extensions.Logging => 195
	i64 u0xdd2b722d78ef5f43, ; 849: System.Runtime.dll => 119
	i64 u0xdd67031857c72f96, ; 850: lib_System.Text.Encodings.Web.dll.so => 139
	i64 u0xdd70765ad6162057, ; 851: Xamarin.JSpecify => 290
	i64 u0xdd92e229ad292030, ; 852: System.Numerics.dll => 86
	i64 u0xdde30e6b77aa6f6c, ; 853: lib-zh-Hans-Microsoft.Maui.Controls.resources.dll.so => 329
	i64 u0xde110ae80fa7c2e2, ; 854: System.Xml.XDocument.dll => 162
	i64 u0xde327cd8af0a2cbc, ; 855: lib_Microsoft.Extensions.Configuration.CommandLine.dll.so => 185
	i64 u0xde4726fcdf63a198, ; 856: Xamarin.AndroidX.Transition => 275
	i64 u0xde572c2b2fb32f93, ; 857: lib_System.Threading.Tasks.Extensions.dll.so => 146
	i64 u0xde8769ebda7d8647, ; 858: hr/Microsoft.Maui.Controls.resources.dll => 308
	i64 u0xdee075f3477ef6be, ; 859: Xamarin.AndroidX.ExifInterface.dll => 237
	i64 u0xdf4b773de8fb1540, ; 860: System.Net.dll => 84
	i64 u0xdf9c7682560a9629, ; 861: System.Net.ServerSentEvents => 76
	i64 u0xdfa254ebb4346068, ; 862: System.Net.Ping => 71
	i64 u0xe0142572c095a480, ; 863: Xamarin.AndroidX.AppCompat.dll => 214
	i64 u0xe021eaa401792a05, ; 864: System.Text.Encoding.dll => 138
	i64 u0xe02f89350ec78051, ; 865: Xamarin.AndroidX.CoordinatorLayout.dll => 226
	i64 u0xe0496b9d65ef5474, ; 866: Xamarin.Android.Glide.DiskLruCache.dll => 207
	i64 u0xe10b760bb1462e7a, ; 867: lib_System.Security.Cryptography.Primitives.dll.so => 127
	i64 u0xe192a588d4410686, ; 868: lib_System.IO.Pipelines.dll.so => 54
	i64 u0xe1a08bd3fa539e0d, ; 869: System.Runtime.Loader => 112
	i64 u0xe1a77eb8831f7741, ; 870: System.Security.SecureString.dll => 132
	i64 u0xe1b52f9f816c70ef, ; 871: System.Private.Xml.Linq.dll => 90
	i64 u0xe1e199c8ab02e356, ; 872: System.Data.DataSetExtensions.dll => 23
	i64 u0xe1ecfdb7fff86067, ; 873: System.Net.Security.dll => 75
	i64 u0xe2252a80fe853de4, ; 874: lib_System.Security.Principal.dll.so => 131
	i64 u0xe22fa4c9c645db62, ; 875: System.Diagnostics.TextWriterTraceListener.dll => 31
	i64 u0xe24095a7afddaab3, ; 876: lib_Microsoft.Extensions.Hosting.Abstractions.dll.so => 194
	i64 u0xe2420585aeceb728, ; 877: System.Net.Requests.dll => 74
	i64 u0xe26692647e6bcb62, ; 878: Xamarin.AndroidX.Lifecycle.Runtime.Ktx => 249
	i64 u0xe29b73bc11392966, ; 879: lib-id-Microsoft.Maui.Controls.resources.dll.so => 310
	i64 u0xe2ad448dee50fbdf, ; 880: System.Xml.Serialization => 161
	i64 u0xe2d920f978f5d85c, ; 881: System.Data.DataSetExtensions => 23
	i64 u0xe2e426c7714fa0bc, ; 882: Microsoft.Win32.Primitives.dll => 4
	i64 u0xe332bacb3eb4a806, ; 883: Mono.Android.Export.dll => 173
	i64 u0xe3811d68d4fe8463, ; 884: pt-BR/Microsoft.Maui.Controls.resources.dll => 318
	i64 u0xe3b7cbae5ad66c75, ; 885: lib_System.Security.Cryptography.Encoding.dll.so => 125
	i64 u0xe4292b48f3224d5b, ; 886: lib_Xamarin.AndroidX.Core.ViewTree.dll.so => 229
	i64 u0xe494f7ced4ecd10a, ; 887: hu/Microsoft.Maui.Controls.resources.dll => 309
	i64 u0xe4a9b1e40d1e8917, ; 888: lib-fi-Microsoft.Maui.Controls.resources.dll.so => 304
	i64 u0xe4f74a0b5bf9703f, ; 889: System.Runtime.Serialization.Primitives => 116
	i64 u0xe5434e8a119ceb69, ; 890: lib_Mono.Android.dll.so => 175
	i64 u0xe55703b9ce5c038a, ; 891: System.Diagnostics.Tools => 32
	i64 u0xe57013c8afc270b5, ; 892: Microsoft.VisualBasic => 3
	i64 u0xe62913cc36bc07ec, ; 893: System.Xml.dll => 167
	i64 u0xe7bea09c4900a191, ; 894: Xamarin.AndroidX.VectorDrawable.dll => 276
	i64 u0xe7e03cc18dcdeb49, ; 895: lib_System.Diagnostics.StackTrace.dll.so => 30
	i64 u0xe7e147ff99a7a380, ; 896: lib_System.Configuration.dll.so => 19
	i64 u0xe86b0df4ba9e5db8, ; 897: lib_Xamarin.AndroidX.Lifecycle.Runtime.Android.dll.so => 248
	i64 u0xe896622fe0902957, ; 898: System.Reflection.Emit.dll => 95
	i64 u0xe89a2a9ef110899b, ; 899: System.Drawing.dll => 36
	i64 u0xe8c5f8c100b5934b, ; 900: Microsoft.Win32.Registry => 5
	i64 u0xe98163eb702ae5c5, ; 901: Xamarin.AndroidX.Arch.Core.Runtime => 217
	i64 u0xe994f23ba4c143e5, ; 902: Xamarin.KotlinX.Coroutines.Android => 292
	i64 u0xe9b9c8c0458fd92a, ; 903: System.Windows => 158
	i64 u0xe9d166d87a7f2bdb, ; 904: lib_Xamarin.AndroidX.Startup.StartupRuntime.dll.so => 271
	i64 u0xea5a4efc2ad81d1b, ; 905: Xamarin.Google.ErrorProne.Annotations => 287
	i64 u0xeb2313fe9d65b785, ; 906: Xamarin.AndroidX.ConstraintLayout.dll => 224
	i64 u0xed19c616b3fcb7eb, ; 907: Xamarin.AndroidX.VersionedParcelable.dll => 278
	i64 u0xedc4817167106c23, ; 908: System.Net.Sockets.dll => 78
	i64 u0xedc632067fb20ff3, ; 909: System.Memory.dll => 64
	i64 u0xedc8e4ca71a02a8b, ; 910: Xamarin.AndroidX.Navigation.Runtime.dll => 260
	i64 u0xee81f5b3f1c4f83b, ; 911: System.Threading.ThreadPool => 150
	i64 u0xeeb7ebb80150501b, ; 912: lib_Xamarin.AndroidX.Collection.Jvm.dll.so => 221
	i64 u0xeefc635595ef57f0, ; 913: System.Security.Cryptography.Cng => 123
	i64 u0xef03b1b5a04e9709, ; 914: System.Text.Encoding.CodePages.dll => 136
	i64 u0xef5bcbe61622ee5f, ; 915: Xamarin.AndroidX.Tracing.Tracing.Android.dll => 274
	i64 u0xef602c523fe2e87a, ; 916: lib_Xamarin.Google.Guava.ListenableFuture.dll.so => 288
	i64 u0xef72742e1bcca27a, ; 917: Microsoft.Maui.Essentials.dll => 203
	i64 u0xefd1e0c4e5c9b371, ; 918: System.Resources.ResourceManager.dll => 102
	i64 u0xefe8f8d5ed3c72ea, ; 919: System.Formats.Tar.dll => 39
	i64 u0xefec0b7fdc57ec42, ; 920: Xamarin.AndroidX.Activity => 209
	i64 u0xeff59cbde4363ec3, ; 921: System.Threading.AccessControl.dll => 142
	i64 u0xf00c29406ea45e19, ; 922: es/Microsoft.Maui.Controls.resources.dll => 303
	i64 u0xf09e47b6ae914f6e, ; 923: System.Net.NameResolution => 69
	i64 u0xf0ac2b489fed2e35, ; 924: lib_System.Diagnostics.Debug.dll.so => 26
	i64 u0xf0bb49dadd3a1fe1, ; 925: lib_System.Net.ServicePoint.dll.so => 77
	i64 u0xf0c16dff90fbf5d6, ; 926: Xamarin.AndroidX.Window.WindowCore.Jvm => 283
	i64 u0xf0de2537ee19c6ca, ; 927: lib_System.Net.WebHeaderCollection.dll.so => 80
	i64 u0xf1138779fa181c68, ; 928: lib_Xamarin.AndroidX.Lifecycle.Runtime.dll.so => 247
	i64 u0xf11b621fc87b983f, ; 929: Microsoft.Maui.Controls.Xaml.dll => 201
	i64 u0xf161f4f3c3b7e62c, ; 930: System.Data => 24
	i64 u0xf16eb650d5a464bc, ; 931: System.ValueTuple => 155
	i64 u0xf1c4b4005493d871, ; 932: System.Formats.Asn1.dll => 38
	i64 u0xf22514cfad2d598b, ; 933: lib_Xamarin.AndroidX.Lifecycle.ViewModelSavedState.Android.dll.so => 255
	i64 u0xf238bd79489d3a96, ; 934: lib-nl-Microsoft.Maui.Controls.resources.dll.so => 316
	i64 u0xf2feea356ba760af, ; 935: Xamarin.AndroidX.Arch.Core.Runtime.dll => 217
	i64 u0xf300e085f8acd238, ; 936: lib_System.ServiceProcess.dll.so => 135
	i64 u0xf34e52b26e7e059d, ; 937: System.Runtime.CompilerServices.VisualC.dll => 105
	i64 u0xf37221fda4ef8830, ; 938: lib_Xamarin.Google.Android.Material.dll.so => 284
	i64 u0xf3ad9b8fb3eefd12, ; 939: lib_System.IO.UnmanagedMemoryStream.dll.so => 57
	i64 u0xf3ddfe05336abf29, ; 940: System => 168
	i64 u0xf408654b2a135055, ; 941: System.Reflection.Emit.ILGeneration.dll => 93
	i64 u0xf4103170a1de5bd0, ; 942: System.Linq.Queryable.dll => 62
	i64 u0xf42d20c23173d77c, ; 943: lib_System.ServiceModel.Web.dll.so => 134
	i64 u0xf4c1dd70a5496a17, ; 944: System.IO.Compression => 46
	i64 u0xf4ecf4b9afc64781, ; 945: System.ServiceProcess.dll => 135
	i64 u0xf4eeeaa566e9b970, ; 946: lib_Xamarin.AndroidX.CustomView.PoolingContainer.dll.so => 232
	i64 u0xf518f63ead11fcd1, ; 947: System.Threading.Tasks => 148
	i64 u0xf5fc7602fe27b333, ; 948: System.Net.WebHeaderCollection => 80
	i64 u0xf6077741019d7428, ; 949: Xamarin.AndroidX.CoordinatorLayout => 226
	i64 u0xf6742cbf457c450b, ; 950: Xamarin.AndroidX.Lifecycle.Runtime.Android.dll => 248
	i64 u0xf6e8de2aebcbb422, ; 951: lib_Xamarin.AndroidX.Window.WindowCore.Jvm.dll.so => 283
	i64 u0xf6e9ed5983c08f64, ; 952: Microsoft.Extensions.Configuration.CommandLine.dll => 185
	i64 u0xf70c0a7bf8ccf5af, ; 953: System.Web => 157
	i64 u0xf77b20923f07c667, ; 954: de/Microsoft.Maui.Controls.resources.dll => 301
	i64 u0xf7e2cac4c45067b3, ; 955: lib_System.Numerics.Vectors.dll.so => 85
	i64 u0xf7e74930e0e3d214, ; 956: zh-HK/Microsoft.Maui.Controls.resources.dll => 328
	i64 u0xf84773b5c81e3cef, ; 957: lib-uk-Microsoft.Maui.Controls.resources.dll.so => 326
	i64 u0xf8aac5ea82de1348, ; 958: System.Linq.Queryable => 62
	i64 u0xf8b77539b362d3ba, ; 959: lib_System.Reflection.Primitives.dll.so => 98
	i64 u0xf8e045dc345b2ea3, ; 960: lib_Xamarin.AndroidX.RecyclerView.dll.so => 264
	i64 u0xf915dc29808193a1, ; 961: System.Web.HttpUtility.dll => 156
	i64 u0xf96c777a2a0686f4, ; 962: hi/Microsoft.Maui.Controls.resources.dll => 307
	i64 u0xf9be54c8bcf8ff3b, ; 963: System.Security.AccessControl.dll => 120
	i64 u0xf9eec5bb3a6aedc6, ; 964: Microsoft.Extensions.Options => 198
	i64 u0xfa0e82300e67f913, ; 965: lib_System.AppContext.dll.so => 6
	i64 u0xfa2fdb27e8a2c8e8, ; 966: System.ComponentModel.EventBasedAsync => 15
	i64 u0xfa3f278f288b0e84, ; 967: lib_System.Net.Security.dll.so => 75
	i64 u0xfa504dfa0f097d72, ; 968: Microsoft.Extensions.FileProviders.Abstractions.dll => 191
	i64 u0xfa5ed7226d978949, ; 969: lib-ar-Microsoft.Maui.Controls.resources.dll.so => 297
	i64 u0xfa645d91e9fc4cba, ; 970: System.Threading.Thread => 149
	i64 u0xfad4d2c770e827f9, ; 971: lib_System.IO.IsolatedStorage.dll.so => 52
	i64 u0xfb06dd2338e6f7c4, ; 972: System.Net.Ping.dll => 71
	i64 u0xfb087abe5365e3b7, ; 973: lib_System.Data.DataSetExtensions.dll.so => 23
	i64 u0xfb846e949baff5ea, ; 974: System.Xml.Serialization.dll => 161
	i64 u0xfbad3e4ce4b98145, ; 975: System.Security.Cryptography.X509Certificates => 128
	i64 u0xfbf0a31c9fc34bc4, ; 976: lib_System.Net.Http.dll.so => 66
	i64 u0xfc6b7527cc280b3f, ; 977: lib_System.Runtime.Serialization.Formatters.dll.so => 114
	i64 u0xfc719aec26adf9d9, ; 978: Xamarin.AndroidX.Navigation.Fragment.dll => 259
	i64 u0xfc82690c2fe2735c, ; 979: Xamarin.AndroidX.Lifecycle.Process.dll => 246
	i64 u0xfc93fc307d279893, ; 980: System.IO.Pipes.AccessControl.dll => 55
	i64 u0xfcd302092ada6328, ; 981: System.IO.MemoryMappedFiles.dll => 53
	i64 u0xfd22f00870e40ae0, ; 982: lib_Xamarin.AndroidX.DrawerLayout.dll.so => 233
	i64 u0xfd49b3c1a76e2748, ; 983: System.Runtime.InteropServices.RuntimeInformation => 109
	i64 u0xfd536c702f64dc47, ; 984: System.Text.Encoding.Extensions => 137
	i64 u0xfd583f7657b6a1cb, ; 985: Xamarin.AndroidX.Fragment => 238
	i64 u0xfd8dd91a2c26bd5d, ; 986: Xamarin.AndroidX.Lifecycle.Runtime => 247
	i64 u0xfda36abccf05cf5c, ; 987: System.Net.WebSockets.Client => 82
	i64 u0xfdbe4710aa9beeff, ; 988: CommunityToolkit.Maui => 178
	i64 u0xfddbe9695626a7f5, ; 989: Xamarin.AndroidX.Lifecycle.Common => 241
	i64 u0xfe9856c3af9365ab, ; 990: lib_Microsoft.Extensions.Configuration.FileExtensions.dll.so => 187
	i64 u0xfeae9952cf03b8cb, ; 991: tr/Microsoft.Maui.Controls.resources => 325
	i64 u0xfebe1950717515f9, ; 992: Xamarin.AndroidX.Lifecycle.LiveData.Core.Ktx.dll => 245
	i64 u0xff270a55858bac8d, ; 993: System.Security.Principal => 131
	i64 u0xff9b54613e0d2cc8, ; 994: System.Net.Http.Json => 65
	i64 u0xffdb7a971be4ec73 ; 995: System.ValueTuple.dll => 155
], align 16

@assembly_image_cache_indices = dso_local local_unnamed_addr constant [996 x i32] [
	i32 42, i32 293, i32 272, i32 13, i32 260, i32 179, i32 107, i32 174,
	i32 48, i32 214, i32 7, i32 88, i32 321, i32 299, i32 327, i32 234,
	i32 72, i32 264, i32 12, i32 202, i32 104, i32 328, i32 159, i32 19,
	i32 239, i32 221, i32 164, i32 236, i32 276, i32 170, i32 321, i32 10,
	i32 197, i32 277, i32 98, i32 232, i32 233, i32 13, i32 198, i32 10,
	i32 129, i32 97, i32 143, i32 39, i32 322, i32 296, i32 279, i32 318,
	i32 175, i32 208, i32 5, i32 203, i32 68, i32 269, i32 132, i32 268,
	i32 235, i32 69, i32 222, i32 67, i32 57, i32 231, i32 52, i32 43,
	i32 127, i32 68, i32 83, i32 249, i32 161, i32 94, i32 101, i32 264,
	i32 144, i32 154, i32 218, i32 305, i32 165, i32 172, i32 306, i32 189,
	i32 83, i32 290, i32 222, i32 4, i32 5, i32 51, i32 103, i32 56,
	i32 122, i32 100, i32 171, i32 120, i32 293, i32 21, i32 309, i32 139,
	i32 99, i32 296, i32 79, i32 315, i32 271, i32 121, i32 8, i32 168,
	i32 324, i32 71, i32 207, i32 250, i32 265, i32 192, i32 174, i32 148,
	i32 40, i32 269, i32 47, i32 30, i32 262, i32 313, i32 147, i32 198,
	i32 166, i32 28, i32 86, i32 273, i32 79, i32 43, i32 29, i32 42,
	i32 105, i32 119, i32 212, i32 45, i32 93, i32 324, i32 56, i32 151,
	i32 149, i32 102, i32 49, i32 20, i32 227, i32 116, i32 205, i32 305,
	i32 286, i32 291, i32 199, i32 96, i32 58, i32 310, i32 308, i32 282,
	i32 83, i32 286, i32 172, i32 26, i32 72, i32 263, i32 190, i32 237,
	i32 258, i32 326, i32 70, i32 33, i32 304, i32 14, i32 141, i32 38,
	i32 330, i32 193, i32 223, i32 317, i32 136, i32 94, i32 90, i32 152,
	i32 283, i32 323, i32 24, i32 140, i32 57, i32 142, i32 51, i32 302,
	i32 29, i32 160, i32 34, i32 167, i32 238, i32 52, i32 331, i32 281,
	i32 92, i32 219, i32 35, i32 305, i32 160, i32 193, i32 9, i32 303,
	i32 78, i32 59, i32 55, i32 202, i32 299, i32 200, i32 13, i32 280,
	i32 182, i32 216, i32 111, i32 253, i32 32, i32 106, i32 86, i32 94,
	i32 53, i32 98, i32 289, i32 58, i32 9, i32 104, i32 231, i32 69,
	i32 279, i32 298, i32 191, i32 127, i32 265, i32 118, i32 137, i32 267,
	i32 128, i32 108, i32 291, i32 133, i32 218, i32 288, i32 150, i32 159,
	i32 239, i32 227, i32 187, i32 234, i32 265, i32 99, i32 24, i32 270,
	i32 146, i32 194, i32 257, i32 3, i32 170, i32 215, i32 102, i32 164,
	i32 101, i32 229, i32 25, i32 95, i32 171, i32 175, i32 210, i32 3,
	i32 317, i32 274, i32 236, i32 1, i32 116, i32 291, i32 239, i32 246,
	i32 33, i32 6, i32 321, i32 159, i32 319, i32 53, i32 87, i32 278,
	i32 185, i32 262, i32 44, i32 245, i32 106, i32 47, i32 140, i32 255,
	i32 65, i32 256, i32 70, i32 82, i32 60, i32 91, i32 157, i32 216,
	i32 135, i32 112, i32 59, i32 311, i32 256, i32 263, i32 174, i32 136,
	i32 143, i32 40, i32 298, i32 184, i32 200, i32 61, i32 180, i32 184,
	i32 252, i32 81, i32 25, i32 36, i32 101, i32 249, i32 72, i32 22,
	i32 227, i32 204, i32 322, i32 123, i32 70, i32 109, i32 328, i32 121,
	i32 119, i32 241, i32 258, i32 242, i32 11, i32 2, i32 126, i32 117,
	i32 145, i32 41, i32 89, i32 211, i32 177, i32 194, i32 27, i32 151,
	i32 184, i32 312, i32 188, i32 287, i32 210, i32 1, i32 212, i32 44,
	i32 226, i32 152, i32 18, i32 88, i32 300, i32 41, i32 245, i32 220,
	i32 250, i32 96, i32 195, i32 28, i32 41, i32 80, i32 235, i32 223,
	i32 147, i32 110, i32 221, i32 11, i32 107, i32 139, i32 16, i32 124,
	i32 67, i32 160, i32 22, i32 302, i32 295, i32 104, i32 188, i32 294,
	i32 64, i32 58, i32 201, i32 301, i32 112, i32 177, i32 261, i32 292,
	i32 9, i32 284, i32 122, i32 100, i32 107, i32 76, i32 253, i32 178,
	i32 200, i32 113, i32 213, i32 49, i32 59, i32 20, i32 252, i32 230,
	i32 73, i32 225, i32 158, i32 39, i32 300, i32 35, i32 38, i32 306,
	i32 110, i32 315, i32 21, i32 289, i32 251, i32 204, i32 15, i32 199,
	i32 81, i32 81, i32 230, i32 199, i32 259, i32 268, i32 155, i32 21,
	i32 202, i32 299, i32 50, i32 51, i32 325, i32 315, i32 96, i32 206,
	i32 190, i32 311, i32 16, i32 229, i32 125, i32 308, i32 163, i32 45,
	i32 287, i32 181, i32 118, i32 64, i32 169, i32 182, i32 14, i32 266,
	i32 113, i32 213, i32 61, i32 76, i32 123, i32 314, i32 2, i32 324,
	i32 238, i32 251, i32 290, i32 251, i32 6, i32 220, i32 304, i32 234,
	i32 17, i32 322, i32 301, i32 79, i32 224, i32 258, i32 179, i32 133,
	i32 289, i32 314, i32 85, i32 197, i32 12, i32 34, i32 121, i32 295,
	i32 246, i32 176, i32 236, i32 87, i32 205, i32 18, i32 279, i32 183,
	i32 244, i32 73, i32 97, i32 168, i32 240, i32 84, i32 330, i32 214,
	i32 219, i32 157, i32 36, i32 154, i32 326, i32 329, i32 0, i32 147,
	i32 56, i32 115, i32 220, i32 276, i32 275, i32 37, i32 330, i32 182,
	i32 117, i32 193, i32 212, i32 14, i32 206, i32 149, i32 43, i32 203,
	i32 210, i32 100, i32 294, i32 171, i32 16, i32 267, i32 48, i32 109,
	i32 99, i32 256, i32 27, i32 130, i32 29, i32 306, i32 191, i32 268,
	i32 130, i32 44, i32 230, i32 235, i32 152, i32 8, i32 274, i32 257,
	i32 307, i32 320, i32 319, i32 134, i32 318, i32 42, i32 295, i32 33,
	i32 331, i32 46, i32 146, i32 252, i32 201, i32 243, i32 231, i32 140,
	i32 63, i32 134, i32 298, i32 48, i32 163, i32 217, i32 243, i32 206,
	i32 241, i32 314, i32 275, i32 46, i32 167, i32 176, i32 240, i32 303,
	i32 237, i32 310, i32 204, i32 18, i32 8, i32 181, i32 228, i32 267,
	i32 126, i32 60, i32 144, i32 180, i32 260, i32 313, i32 247, i32 285,
	i32 281, i32 153, i32 145, i32 293, i32 128, i32 292, i32 163, i32 165,
	i32 232, i32 209, i32 183, i32 261, i32 316, i32 26, i32 257, i32 244,
	i32 192, i32 84, i32 281, i32 129, i32 286, i32 103, i32 151, i32 284,
	i32 282, i32 262, i32 54, i32 165, i32 170, i32 133, i32 37, i32 277,
	i32 313, i32 180, i32 22, i32 114, i32 92, i32 50, i32 61, i32 124,
	i32 85, i32 129, i32 166, i32 285, i32 169, i32 266, i32 269, i32 233,
	i32 205, i32 248, i32 4, i32 242, i32 309, i32 173, i32 2, i32 192,
	i32 253, i32 118, i32 211, i32 19, i32 179, i32 196, i32 91, i32 66,
	i32 30, i32 189, i32 302, i32 0, i32 0, i32 225, i32 60, i32 113,
	i32 244, i32 32, i32 130, i32 162, i32 320, i32 223, i32 143, i32 316,
	i32 156, i32 17, i32 222, i32 208, i32 77, i32 75, i32 15, i32 172,
	i32 87, i32 126, i32 243, i32 254, i32 186, i32 224, i32 323, i32 250,
	i32 34, i32 120, i32 141, i32 124, i32 108, i32 300, i32 277, i32 219,
	i32 307, i32 297, i32 54, i32 47, i32 28, i32 142, i32 148, i32 196,
	i32 150, i32 35, i32 323, i32 177, i32 77, i32 164, i32 1, i32 255,
	i32 270, i32 319, i32 312, i32 162, i32 12, i32 158, i32 186, i32 154,
	i32 78, i32 105, i32 114, i32 216, i32 66, i32 67, i32 280, i32 45,
	i32 218, i32 111, i32 7, i32 215, i32 55, i32 211, i32 65, i32 297,
	i32 228, i32 20, i32 111, i32 103, i32 63, i32 145, i32 209, i32 7,
	i32 186, i32 312, i32 173, i32 50, i32 280, i32 117, i32 144, i32 181,
	i32 169, i32 82, i32 115, i32 254, i32 17, i32 74, i32 259, i32 91,
	i32 207, i32 89, i32 122, i32 273, i32 187, i32 213, i32 261, i32 137,
	i32 156, i32 108, i32 11, i32 92, i32 31, i32 178, i32 325, i32 138,
	i32 317, i32 320, i32 271, i32 208, i32 40, i32 331, i32 270, i32 141,
	i32 294, i32 296, i32 25, i32 176, i32 329, i32 74, i32 240, i32 272,
	i32 27, i32 68, i32 90, i32 97, i32 115, i32 31, i32 106, i32 242,
	i32 37, i32 190, i32 73, i32 288, i32 110, i32 125, i32 215, i32 89,
	i32 195, i32 88, i32 311, i32 95, i32 189, i32 131, i32 254, i32 273,
	i32 197, i32 266, i32 228, i32 272, i32 225, i32 285, i32 183, i32 166,
	i32 132, i32 196, i32 278, i32 282, i32 263, i32 188, i32 10, i32 49,
	i32 327, i32 93, i32 327, i32 153, i32 63, i32 138, i32 153, i32 62,
	i32 195, i32 119, i32 139, i32 290, i32 86, i32 329, i32 162, i32 185,
	i32 275, i32 146, i32 308, i32 237, i32 84, i32 76, i32 71, i32 214,
	i32 138, i32 226, i32 207, i32 127, i32 54, i32 112, i32 132, i32 90,
	i32 23, i32 75, i32 131, i32 31, i32 194, i32 74, i32 249, i32 310,
	i32 161, i32 23, i32 4, i32 173, i32 318, i32 125, i32 229, i32 309,
	i32 304, i32 116, i32 175, i32 32, i32 3, i32 167, i32 276, i32 30,
	i32 19, i32 248, i32 95, i32 36, i32 5, i32 217, i32 292, i32 158,
	i32 271, i32 287, i32 224, i32 278, i32 78, i32 64, i32 260, i32 150,
	i32 221, i32 123, i32 136, i32 274, i32 288, i32 203, i32 102, i32 39,
	i32 209, i32 142, i32 303, i32 69, i32 26, i32 77, i32 283, i32 80,
	i32 247, i32 201, i32 24, i32 155, i32 38, i32 255, i32 316, i32 217,
	i32 135, i32 105, i32 284, i32 57, i32 168, i32 93, i32 62, i32 134,
	i32 46, i32 135, i32 232, i32 148, i32 80, i32 226, i32 248, i32 283,
	i32 185, i32 157, i32 301, i32 85, i32 328, i32 326, i32 62, i32 98,
	i32 264, i32 156, i32 307, i32 120, i32 198, i32 6, i32 15, i32 75,
	i32 191, i32 297, i32 149, i32 52, i32 71, i32 23, i32 161, i32 128,
	i32 66, i32 114, i32 259, i32 246, i32 55, i32 53, i32 233, i32 109,
	i32 137, i32 238, i32 247, i32 82, i32 178, i32 241, i32 187, i32 325,
	i32 245, i32 131, i32 65, i32 155
], align 16

@marshal_methods_number_of_classes = dso_local local_unnamed_addr constant i32 0, align 4

@marshal_methods_class_cache = dso_local local_unnamed_addr global [0 x %struct.MarshalMethodsManagedClass] zeroinitializer, align 8

; Names of classes in which marshal methods reside
@mm_class_names = dso_local local_unnamed_addr constant [0 x ptr] zeroinitializer, align 8

@mm_method_names = dso_local local_unnamed_addr constant [1 x %struct.MarshalMethodName] [
	%struct.MarshalMethodName {
		i64 u0x0000000000000000, ; name: 
		ptr @.MarshalMethodName.0_name; char* name
	} ; 0
], align 8

; get_function_pointer (uint32_t mono_image_index, uint32_t class_index, uint32_t method_token, void*& target_ptr)
@get_function_pointer = internal dso_local unnamed_addr global ptr null, align 8

; Functions

; Function attributes: memory(write, argmem: none, inaccessiblemem: none) "min-legal-vector-width"="0" mustprogress nofree norecurse nosync "no-trapping-math"="true" nounwind "stack-protector-buffer-size"="8" uwtable willreturn
define void @xamarin_app_init(ptr nocapture noundef readnone %env, ptr noundef %fn) local_unnamed_addr #0
{
	%fnIsNull = icmp eq ptr %fn, null
	br i1 %fnIsNull, label %1, label %2

1: ; preds = %0
	%putsResult = call noundef i32 @puts(ptr @.mm.0)
	call void @abort()
	unreachable 

2: ; preds = %1, %0
	store ptr %fn, ptr @get_function_pointer, align 8, !tbaa !3
	ret void
}

; Strings
@.mm.0 = private unnamed_addr constant [40 x i8] c"get_function_pointer MUST be specified\0A\00", align 16

;MarshalMethodName
@.MarshalMethodName.0_name = private unnamed_addr constant [1 x i8] c"\00", align 1

; External functions

; Function attributes: noreturn "no-trapping-math"="true" nounwind "stack-protector-buffer-size"="8"
declare void @abort() local_unnamed_addr #2

; Function attributes: nofree nounwind
declare noundef i32 @puts(ptr noundef) local_unnamed_addr #1
attributes #0 = { memory(write, argmem: none, inaccessiblemem: none) "min-legal-vector-width"="0" mustprogress nofree norecurse nosync "no-trapping-math"="true" nounwind "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+crc32,+cx16,+cx8,+fxsr,+mmx,+popcnt,+sse,+sse2,+sse3,+sse4.1,+sse4.2,+ssse3,+x87" "tune-cpu"="generic" uwtable willreturn }
attributes #1 = { nofree nounwind }
attributes #2 = { noreturn "no-trapping-math"="true" nounwind "stack-protector-buffer-size"="8" "target-cpu"="x86-64" "target-features"="+crc32,+cx16,+cx8,+fxsr,+mmx,+popcnt,+sse,+sse2,+sse3,+sse4.1,+sse4.2,+ssse3,+x87" "tune-cpu"="generic" }

; Metadata
!llvm.module.flags = !{!0, !1}
!0 = !{i32 1, !"wchar_size", i32 4}
!1 = !{i32 7, !"PIC Level", i32 2}
!llvm.ident = !{!2}
!2 = !{!".NET for Android remotes/origin/darc-release/10.0.1xx-fce6efd9-cc42-423a-a4db-1a5ebe0f4ee4 @ 350a375fc202f0072ac4191624986d8c642b93fa"}
!3 = !{!4, !4, i64 0}
!4 = !{!"any pointer", !5, i64 0}
!5 = !{!"omnipotent char", !6, i64 0}
!6 = !{!"Simple C++ TBAA"}

// ***********************************************************************
//  Assembly         : RzR.DataVigil
//  Author           : RzR
//  Created On       : 2026-01-18 17:01
// 
//  Last Modified By : RzR
//  Last Modified On : 2026-01-18 18:53
// ***********************************************************************
//  <copyright file="GeneralAssemblyInfo.cs" company="RzR SOFT & TECH">
//   Copyright © RzR. All rights reserved.
//  </copyright>
// 
//  <summary>
//  </summary>
// ***********************************************************************

#region U S A G E S

using System.Reflection;
using System.Resources;

#endregion

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif

[assembly: AssemblyCompany("RzR ®")]
[assembly: AssemblyProduct("RzR.DataVigil")]
[assembly: AssemblyCopyright("Copyright © 2022-2026 RzR All rights reserved.")]
[assembly: AssemblyTrademark("® RzR™")]
[assembly: AssemblyDescription("A .NET audit trail framework that automatically captures, protects, and stores every data change with built-in GDPR compliance, EF Core integration, and pluggable storage backends.")]

#if NET45_OR_GREATER || NET || NETSTANDARD
[assembly: AssemblyMetadata("TermsOfService", "")]

[assembly: AssemblyMetadata("ContactUrl", "iamrzr.dev/contact")]
[assembly: AssemblyMetadata("ContactName", "RzR")]
[assembly: AssemblyMetadata("ContactEmail", "ddpRzR@hotmail.com")]
#endif
#if NETSTANDARD1_6_OR_GREATER || NET35_OR_GREATER
[assembly: NeutralResourcesLanguage("en-US", UltimateResourceFallbackLocation.MainAssembly)]
#endif

[assembly: AssemblyVersion("1.2.1.4420")]
[assembly: AssemblyFileVersion("1.2.1.4420")]
[assembly: AssemblyInformationalVersion("1.2.1.4420")]

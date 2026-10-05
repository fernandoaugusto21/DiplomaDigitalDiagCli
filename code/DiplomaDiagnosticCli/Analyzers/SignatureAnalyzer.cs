using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using DiplomaDiagnosticCli.Documents;
using DiplomaDiagnosticCli.Models;
using DiplomaDiagnosticCli.Signatures;

namespace DiplomaDiagnosticCli.Analyzers;

/// <summary>
/// Inventories every ds:Signature (who signed which node, when, under which policy) and verifies its
/// XMLDSig core (reference digests + signature value against the embedded certificate). ICP-Brasil
/// chain, revocation and timestamp validation are out of scope and reported as "unvalidated".
/// Then checks the signature layout expected for each document type.
/// </summary>
public sealed class SignatureAnalyzer : IDocumentAnalyzer
{
    private const string SignedPropertiesType = "http://uri.etsi.org/01903#SignedProperties";

    public void Analyze(MecDocument document, AnalysisContext context)
    {
        var originals = document.Elements("//ds:Signature", document.Xml).ToList();
        if (originals.Count == 0)
        {
            if (document.Kind != DocumentKind.Unsupported)
                context.Error("SIG_NONE", "The document has no ds:Signature at all.", $"element::{document.RootName}",
                    "An unsigned Diploma/Histórico has no legal validity; the XSD also requires at least one signature.");
            return;
        }

        XmlDocument verificationCopy = MecSignedXml.PrepareForVerification(document);
        var copyNs = new XmlNamespaceManager(verificationCopy.NameTable);
        copyNs.AddNamespace("ds", MecDocument.DsNamespace);
        var copies = verificationCopy.SelectNodes("//ds:Signature", copyNs)!.OfType<XmlElement>().ToList();

        for (int i = 0; i < originals.Count; i++)
            context.Signatures.Add(Inspect(i + 1, originals[i], copies[i], verificationCopy, document, context));

        CheckLayout(document, context);
    }

    private static SignatureDto Inspect(int index, XmlElement sig, XmlElement sigCopy, XmlDocument verificationCopy,
        MecDocument document, AnalysisContext context)
    {
        var ns = document.Ns;
        string parent = sig.ParentNode?.LocalName ?? "(none)";
        string where = $"signature::{index}({parent})";

        var dataRefs = sig.SelectNodes("ds:SignedInfo/ds:Reference", ns)!.OfType<XmlElement>()
            .Where(r => r.GetAttribute("Type") != SignedPropertiesType).ToList();
        bool hasSignedProperties = sig.SelectNodes("ds:SignedInfo/ds:Reference", ns)!.OfType<XmlElement>()
            .Any(r => r.GetAttribute("Type") == SignedPropertiesType);

        var uris = dataRefs.Select(r => r.GetAttribute("URI")).ToList();
        var signedElements = new List<string>();
        foreach (string uri in uris)
        {
            if (uri.Length == 0)
            {
                signedElements.Add("(document)");
                continue;
            }
            string id = uri.TrimStart('#');
            var target = id.Contains('\'') ? null
                : document.Xml.SelectSingleNode($"//*[@id='{id}' or @Id='{id}' or @ID='{id}']") as XmlElement;
            if (target is null)
            {
                signedElements.Add($"(unresolved {uri})");
                context.Error("SIG_REFERENCE_UNRESOLVED", $"Signature {index} references '{uri}', which matches no element id.", where);
            }
            else
            {
                signedElements.Add(target.LocalName);
            }
        }

        var transforms = sig.SelectNodes("ds:SignedInfo/ds:Reference/ds:Transforms/ds:Transform", ns)!.OfType<XmlElement>().ToList();
        var transformNames = transforms.Select(t => t.GetAttribute("Algorithm")).Distinct().ToList();
        var foreignXPaths = transforms
            .Select(t => t.SelectSingleNode("ds:XPath", ns)?.InnerText.Trim())
            .Where(x => x is not null && x != MecSignedXml.AllowedXPath)
            .ToList();

        // Certificate
        X509Certificate2? cert = null;
        string? certText = sig.SelectSingleNode("ds:KeyInfo/ds:X509Data/ds:X509Certificate", ns)?.InnerText;
        if (certText is not null)
        {
            try { cert = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(certText.Trim())); }
            catch (Exception ex) { context.Error("SIG_CERTIFICATE_UNREADABLE", $"Signature {index}: embedded certificate cannot be decoded ({ex.Message}).", where); }
        }

        // XMLDSig core verification
        bool valid = false;
        string? failure = null;
        if (foreignXPaths.Count > 0)
        {
            failure = $"unsupported XPath transform: {foreignXPaths[0]}";
            context.Error("SIG_TRANSFORM_UNSUPPORTED",
                $"Signature {index} uses an XPath transform other than '{MecSignedXml.AllowedXPath}'; it was not executed (arbitrary XPath from an untrusted file).",
                where);
        }
        else if (cert is null)
        {
            failure = "no X509Certificate in KeyInfo";
            context.Error("SIG_NO_CERTIFICATE", $"Signature {index} carries no certificate in KeyInfo; it cannot be verified.", where);
        }
        else
        {
            try
            {
                var signedXml = new MecSignedXml(verificationCopy);
                signedXml.LoadXml(sigCopy);
                valid = signedXml.CheckSignature(cert, verifySignatureOnly: true);
                if (!valid)
                    failure = "digest or signature value mismatch";
            }
            catch (Exception ex)
            {
                failure = $"{ex.GetType().Name}: {ex.Message}";
            }

            if (!valid)
            {
                context.Error("SIG_XMLDSIG_INVALID",
                    $"Signature {index} (in {parent}, covering {string.Join(", ", signedElements)}) does not verify: {failure}. " +
                    "The signed content was changed after signing, or the signature is corrupt.",
                    where,
                    "Compare with the other signatures: the ones that still verify show which nodes are intact.");
            }
        }

        // XAdES signed properties
        string? signingTime = sig.SelectSingleNode(".//xades:SigningTime", ns)?.InnerText.Trim();
        string? oid = sig.SelectSingleNode(".//xades:SigPolicyId/xades:Identifier", ns)?.InnerText.Trim();
        if (oid is not null && oid.StartsWith("urn:oid:", StringComparison.OrdinalIgnoreCase))
            oid = oid[8..];
        string? spUri = sig.SelectSingleNode(".//xades:SPURI", ns)?.InnerText.Trim();
        PolicyDto? policy = oid is null && spUri is null ? null : new PolicyDto
        {
            Oid = oid,
            Uri = spUri,
            Name = spUri is null ? null : Path.GetFileNameWithoutExtension(spUri)
        };

        SignerDto? signer = null;
        if (cert is not null)
        {
            var (name, docNumber) = CertificateSubject.SplitCommonName(cert.Subject);
            string type = CertificateSubject.CertificateType(cert);
            signer = new SignerDto
            {
                Name = type == "e-CPF" ? context.Redactor.Name(name)! : name,
                CertificateType = type,
                Document = docNumber is { Length: 11 } ? context.Redactor.Cpf(docNumber) : docNumber,
                // Scrubbed too: a self-signed certificate has the holder as its own issuer.
                Issuer = context.Redactor.Scrub(CertificateSubject.ExtractCn(cert.Issuer)),
                NotBefore = cert.NotBefore.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                NotAfter = cert.NotAfter.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            if (TryParseTime(signingTime) is { } at && (at < cert.NotBefore.ToUniversalTime() || at > cert.NotAfter.ToUniversalTime()))
            {
                context.Error("SIG_CERT_NOT_VALID_AT_SIGNING",
                    $"Signature {index} claims SigningTime {signingTime}, outside the certificate validity ({signer.NotBefore} .. {signer.NotAfter}).",
                    where);
            }
        }

        if (signingTime is null)
            context.Warning("SIG_NO_SIGNING_TIME", $"Signature {index} has no xades:SigningTime; signature order cannot be checked.", where);
        if (!hasSignedProperties)
            context.Warning("SIG_NOT_XADES", $"Signature {index} has no reference to xades:SignedProperties; it is plain XMLDSig, not XAdES.", where,
                "The MEC requires XAdES signatures (ICP-Brasil signature policies).");

        context.Trace("TRACE", $"Signature {index} ({parent}): xmldsig_valid={valid}");

        return new SignatureDto
        {
            Index = index,
            ParentElement = parent,
            SignedElements = signedElements,
            ReferenceUris = uris,
            Transforms = transformNames,
            Signer = signer,
            SigningTime = signingTime,
            Policy = policy,
            HasSignedProperties = hasSignedProperties,
            XmldsigValid = valid,
            FailureReason = failure
        };
    }

    private static void CheckLayout(MecDocument document, AnalysisContext context)
    {
        var sigs = context.Signatures;
        if (document.Kind == DocumentKind.Diploma)
        {
            var emissora = sigs.Where(s => s.ParentElement == "DadosDiploma").ToList();
            var registro = sigs.Where(s => s.ParentElement == "DadosRegistro").ToList();
            var envelope = sigs.Where(s => s.ParentElement == document.RootName).ToList();

            if (emissora.Count == 0)
                context.Error("SIG_MISSING_DADOS_DIPLOMA", "No signature inside DadosDiploma (IES emissora signers).", "element::DadosDiploma");
            else if (!emissora.Any(s => s.Signer?.CertificateType == "e-CNPJ"))
                context.Warning("SIG_EMISSORA_ECNPJ_MISSING", "DadosDiploma has no e-CNPJ signature (the institutional signature of the IES emissora).", "element::DadosDiploma");

            if (registro.Count == 0)
                context.Error("SIG_MISSING_DADOS_REGISTRO", "No signature inside DadosRegistro (IES registradora signers).", "element::DadosRegistro");

            if (envelope.Count == 0)
                context.Error("SIG_MISSING_ENVELOPE", "No envelope signature on the Diploma root (IES registradora, e-CNPJ, AD-RA).", "element::Diploma");
            else
            {
                var env = envelope[^1];
                if (env.Signer is { CertificateType: not "e-CNPJ" })
                    context.Warning("SIG_ENVELOPE_NOT_ECNPJ", $"Envelope signature is {env.Signer.CertificateType}; expected the IES registradora's e-CNPJ.", $"signature::{env.Index}({env.ParentElement})");
                if (env.Policy?.Name is { } name && !name.Contains("AD_RA", StringComparison.OrdinalIgnoreCase))
                    context.Warning("SIG_ENVELOPE_POLICY_NOT_ADRA", $"Envelope signature policy is {name}; the envelope must use AD-RA (long-term archival).", $"signature::{env.Index}({env.ParentElement})");
                if (sigs[^1] != env)
                    context.Error("SIG_ENVELOPE_NOT_LAST", "The envelope signature is not the last signature in the document.", $"signature::{env.Index}({env.ParentElement})");
            }

            CheckStageOrder(context, "DadosDiploma", emissora, "DadosRegistro", registro);
            CheckStageOrder(context, "DadosRegistro", registro, "envelope", envelope);
        }
        else if (document.IsHistorico)
        {
            bool requiresInstitution = document.Kind != DocumentKind.HistoricoParcial;
            if (requiresInstitution && sigs.Count < 2)
                context.Warning("SIG_HISTORICO_COUNT",
                    $"{document.RootName} has {sigs.Count} signature(s); a final/segunda-via histórico is signed by the secretaria representative (e-CPF) and by the IES (e-CNPJ).",
                    $"element::{document.RootName}");
            if (requiresInstitution && sigs[^1].Signer is { CertificateType: not "e-CNPJ" } last)
                context.Warning("SIG_LAST_NOT_ECNPJ", $"The last signature is {last.CertificateType}; expected the IES emissora's e-CNPJ closing the document.", $"signature::{sigs[^1].Index}({sigs[^1].ParentElement})");

            for (int i = 1; i < sigs.Count; i++)
            {
                if (TryParseTime(sigs[i - 1].SigningTime) is { } prev && TryParseTime(sigs[i].SigningTime) is { } cur && cur < prev)
                    context.Warning("SIG_ORDER_INVALID", $"Signature {sigs[i].Index} is dated before signature {sigs[i - 1].Index} although it comes after it in the document.", $"signature::{sigs[i].Index}({sigs[i].ParentElement})");
            }
        }
    }

    private static void CheckStageOrder(AnalysisContext context, string firstName, List<SignatureDto> first, string secondName, List<SignatureDto> second)
    {
        var firstLatest = first.Select(s => TryParseTime(s.SigningTime)).Where(t => t is not null).Max();
        var secondEarliest = second.Select(s => TryParseTime(s.SigningTime)).Where(t => t is not null).Min();
        if (firstLatest is not null && secondEarliest is not null && secondEarliest < firstLatest)
        {
            context.Error("SIG_ORDER_INVALID",
                $"A {secondName} signature ({secondEarliest:yyyy-MM-ddTHH:mm:ssZ}) predates the last {firstName} signature ({firstLatest:yyyy-MM-ddTHH:mm:ssZ}); the {secondName} stage must sign after the {firstName} stage.",
                $"element::{secondName}");
        }
    }

    private static DateTime? TryParseTime(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;
}

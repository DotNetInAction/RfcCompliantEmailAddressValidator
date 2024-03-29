﻿using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Email
{    
    /**
     * To validate an email address according to RFCs 5321, 5322 and others
    **/
    
    /// <summary>
    /// This appears to be RFC 6530 compliant.
    /// </summary>
    public class RfcCompliantEmailAddressValidator
    {
        public MailServerCharacterSetCompliance Compliance = MailServerCharacterSetCompliance.RFC6530;

        private static readonly Regex _regex0To9 = new Regex("^[0-9]+$");
        private static readonly Regex _regexAddressLiteral = new Regex("^\\[(.)+]$");
        private static readonly Regex _regexBadChars = new Regex("[\\x00-\\x20\\(\\)<>\\[\\]:;@\\\\,\\.\"]|^-|-$");
        private static readonly Regex _regexComment = new Regex("(?<!\\\\)(?:[\\(\\)])");
        private static readonly Regex _regexComment1 = new Regex("(?<!\\\\)[\\(\\)]");
        private static readonly Regex _regexGoodStuff = new Regex("^[0-9A-Fa-f]{0,4}$");
        private static readonly Regex _regexQuot = new Regex("(?s)^\"(?:.)*\"$");
        private static readonly Regex _regexQuot2 = new Regex("(?<!\\\\|^)[\"\\r\\n\\x00](?!$)|\\\\\"$|\"\"");
        private static readonly Regex _regexQuot3 = new Regex("[\\x00-\\x20\\(\\)<>\\[\\]:;@\\\\,\\.\"]");
        private static readonly Regex _regexSlash = new Regex("\\\\\\\\");
        private static readonly Regex _regexSpamHouses = new Regex(@"\@spamhaus\.", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex _regexSplit = new Regex("(?m)\\.(?=(?:[^\\\"]*\\\"[^\\\"]*\\\")*(?![^\\\"]*\\\"))");
        private static readonly Regex _regexSplitAddress = new Regex("\\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
        private static RfcCompliantEmailAddressValidator validator;

        public static RfcCompliantEmailAddressValidator Instance
        {
            get
            {
                if (validator == null)
                    validator = new RfcCompliantEmailAddressValidator();
                return validator;
            }
        }

        public List<string> ResultInfo { get; set; }

        /**
         * Checks that an email address conforms to RFCs 5321, 5322 and others. With
         * verbose information.
         *
         * @param email
         *            The email address to check
         * @param checkDNS
         *            If true then a DNS check for A and MX records will be made
         * @return Result-Object of the email analysis.
         * @throws DNSLookupException
         *             Is thrown if an internal error in the DNS lookup appeared.
         */

        public bool IsEmailValid(String email)
        {
            ResultInfo = new List<string>();

            if (email == null)
            {
                email = string.Empty;
            }

            // Check that $email is a valid address. Read the following RFCs to
            // understand the constraints:
            // (http://tools.ietf.org/html/rfc5321)
            // (http://tools.ietf.org/html/rfc5322)
            // (http://tools.ietf.org/html/rfc4291#section-2.2)
            // (http://tools.ietf.org/html/rfc1123#section-2.1)
            // (http://tools.ietf.org/html/rfc3696) (guidance only)

            // the upper limit on address lengths should normally be considered to
            // be 254
            // (http://www.rfc-editor.org/errata_search.php?rfc=3696)
            // NB My erratum has now been verified by the IETF so the correct answer
            // is 254
            //
            // The maximum total length of a reverse-path or forward-path is 256
            // characters (including the punctuation and element separators)
            // (http://tools.ietf.org/html/rfc5321#section-4.5.3.1.3)
            // NB There is a mandatory 2-character wrapper round the actual address
            int emailLength = email.Length;

            // revision 1.17: Max length reduced to 254 (see above)
            if (emailLength > 254)
            {
                this.ResultInfo.Add(@"Email is too long.

                The maximum total length of a reverse-path or forward-path is 256
                characters (including the punctuation and element separators)
                (http://tools.ietf.org/html/rfc5321#section-4.5.3.1.3)
                ");
                return false;
            }

            // Contemporary email addresses consist of a "local part" separated from
            // a "domain part" (a fully-qualified domain name) by an at-sign ("@").
            // (http://tools.ietf.org/html/rfc3696#section-3)
            int atIndex = email.LastIndexOf('@');

            if (atIndex == -1)
            {
                this.ResultInfo.Add(@"Email is too long.

            Contemporary email addresses consist of a ""local part"" separated from
            a ""domain part"" (a fully-qualified domain name) by an at-sign (""@"").
            (http://tools.ietf.org/html/rfc3696#section-3)
            ");
                return false;
            }
            if (atIndex == 0)
            {
                this.ResultInfo.Add(@"Email is too long.

            Contemporary email addresses consist of a ""local part"" separated from
            a ""domain part"" (a fully-qualified domain name) by an at-sign (""@"").
            (http://tools.ietf.org/html/rfc3696#section-3)
            ");
                return false;
            }
            if (atIndex == emailLength - 1)
            {
                this.ResultInfo.Add(@"Email is too long.

            Contemporary email addresses consist of a ""local part"" separated from
            a ""domain part"" (a fully-qualified domain name) by an at-sign (""@"").
            (http://tools.ietf.org/html/rfc3696#section-3)
            ");
                return false;
            }

            // Sanitize comments
            // - remove nested comments, quotes and dots in comments
            // - remove parentheses and dots from quoted strings
            int braceDepth = 0;
            bool inQuote = false;
            bool escapeThisChar = false;

            for (int i = 0; i < emailLength; ++i)
            {
                char charX = email.ToCharArray()[i];
                bool replaceChar = false;

                if (charX == '\\')
                {
                    escapeThisChar = !escapeThisChar; // Escape the next character?
                }
                else
                {
                    switch (charX)
                    {
                        case '(':
                            if (escapeThisChar)
                            {
                                replaceChar = true;
                            }
                            else
                            {
                                if (inQuote)
                                {
                                    replaceChar = true;
                                }
                                else
                                {
                                    if (braceDepth++ > 0)
                                    {
                                        replaceChar = true; // Increment brace depth
                                    }
                                }
                            }

                            break;

                        case ')':
                            if (escapeThisChar)
                            {
                                replaceChar = true;
                            }
                            else
                            {
                                if (inQuote)
                                {
                                    replaceChar = true;
                                }
                                else
                                {
                                    if (--braceDepth > 0)
                                        replaceChar = true; // Decrement brace depth
                                    if (braceDepth < 0)
                                    {
                                        braceDepth = 0;
                                    }
                                }
                            }

                            break;

                        case '"':
                            if (escapeThisChar)
                            {
                                replaceChar = true;
                            }
                            else
                            {
                                if (braceDepth == 0)
                                {
                                    // Are we inside a quoted string?
                                    inQuote = !inQuote;
                                }
                                else
                                {
                                    replaceChar = true;
                                }
                            }

                            break;

                        case '.': // Dots don't help us either
                            if (escapeThisChar)
                            {
                                replaceChar = true;
                            }
                            else
                            {
                                if (braceDepth > 0)
                                    replaceChar = true;
                            }

                            break;
                    }

                    escapeThisChar = false;
                    if (replaceChar)
                    {
                        // Replace the offending character with something harmless
                        // revision 1.12: Line above replaced because PHPLint
                        // doesn't like that syntax
                        email = ReplaceCharAt(email, i, 'x');
                    }
                }
            }

            String localPart = email.Substring(0, atIndex);
            String domain = email.Substring(atIndex + 1);

            // Folding white space
            String foldingWhiteSpace = "(?:(?:(?:[ \\t]*(?:\\r\\n))?[ \\t]+)|(?:[ \\t]+(?:(?:\\r\\n)[ \\t]+)*))";

            // Let's check the local part for RFC compliance...
            //
            // local-part = dot-atom / quoted-string / obs-local-part
            // obs-local-part = word *("." word)
            // (http://tools.ietf.org/html/rfc5322#section-3.4.1)
            //
            // Problem: need to distinguish between "first.last" and "first"."last"
            // (i.e. one element or two). And I suck at regular expressions.

            String[] dotArray = _regexSplit.Split(localPart);
            int partLength = 0;

            #region foreach block

            foreach (String element in dotArray)
            {
                string workingElement = element; // for use in our for loop, can't work on a foreach target SCO-04152011

                // Remove any leading or trailing FWS
                Regex repRegex = new Regex("^" + foldingWhiteSpace + "|" + foldingWhiteSpace + "$");
                String newElement = repRegex.Replace(workingElement, string.Empty);

                if (!workingElement.Equals(newElement))
                {
                    // FWS is unlikely in the real world
                    this.ResultInfo.Add(@"
                    Folding White Space
                        local-part = dot-atom / quoted-string / obs-local-part
                        obs-local-part = word *(""."" word)
                        (http://tools.ietf.org/html/rfc5322#section-3.4.1)
                    ");
                }
                workingElement = newElement; // version 2.3: Warning condition added

                int elementLength = newElement.Length;

                if (elementLength == 0)
                {
                    // Can't have empty element (consecutive dots or
                    // dots at the start or end)
                    this.ResultInfo.Add(@"
                        Can't have empty element (consecutive dots or
                        dots at the start or end)
                        (http://tools.ietf.org/html/rfc5322#section-3.4.1)
                    ");
                    return false;
                }

                // revision 1.15: Speed up the test and get rid of
                // "uninitialized string offset" notices from PHP

                // We need to remove any valid comments (i.e. those at the start or
                // end of the element)
                if (workingElement.Substring(0) == "(")
                {
                    // Comments are unlikely in the real world
                    // return_status = IsEMailResult.ISEMAIL_COMMENTS;

                    // version 2.0: Warning condition added
                    int indexBrace = workingElement.IndexOf(")");
                    if (indexBrace != -1)
                    {
                        if (_regexComment1.Matches(workingElement.Substring(1, indexBrace - 1)).Count > 0)
                        {
                            // Illegal characters in comment
                            this.ResultInfo.Add(@"
                                            Illegal characters in comment
                        ");
                            return false;
                        }
                        workingElement = workingElement.Substring(indexBrace + 1, elementLength - indexBrace - 1);
                        elementLength = workingElement.Length;
                    }
                }

                if (workingElement.Substring(elementLength - 1) == ")")
                {
                    // Comments are unlikely in the real world
                    // return_status = IsEMailResult.ISEMAIL_COMMENTS;

                    // version 2.0: Warning condition added
                    int indexBrace = workingElement.LastIndexOf("(");
                    if (indexBrace != -1)
                    {
                        if (_regexComment.Matches(workingElement.Substring(indexBrace + 1, elementLength - indexBrace - 2)).Count > 0)
                        {
                            // Illegal characters in comment
                            this.ResultInfo.Add(@"
                                            Illegal characters in comment
                        ");
                            return false;
                        }
                        workingElement = workingElement.Substring(0, indexBrace);
                        elementLength = workingElement.Length;
                    }
                }

                // Remove any remaining leading or trailing FWS around the element
                // (having removed any comments)
                Regex fwsRegex = new Regex("^" + foldingWhiteSpace + "|" + foldingWhiteSpace + "$");

                newElement = fwsRegex.Replace(workingElement, string.Empty);

                //// FWS is unlikely in the real world
                //if (!working_element.equals(new_element))
                //    return_status = IsEMailResult.ISEMAIL_FWS;

                workingElement = newElement;

                // version 2.0: Warning condition added

                // What's left counts towards the maximum length for this part
                if (partLength > 0)
                    partLength++; // for the dot
                partLength += workingElement.Length;

                // Each dot-delimited component can be an atom or a quoted string
                // (because of the obs-local-part provision)

                if (_regexQuot.Matches(workingElement).Count > 0)
                {
                    // Quoted-string tests:
                    // Quoted string is unlikely in the real world
                    // return_status = IsEMailResult.ISEMAIL_QUOTEDSTRING;
                    // version 2.0: Warning condition added
                    // Remove any FWS
                    // A warning condition, but we've already raised
                    // ISEMAIL_QUOTEDSTRING
                    Regex newRepRegex = new Regex("(?<!\\\\)" + foldingWhiteSpace);
                    workingElement = newRepRegex.Replace(workingElement, string.Empty);

                    // My regular expression skills aren't up to distinguishing
                    // between \" \\" \\\" \\\\" etc.
                    // So remove all \\ from the string first...
                    workingElement = _regexSlash.Replace(workingElement, string.Empty);

                    if (_regexQuot2.Matches(workingElement).Count > 0)
                    {
                        // ", CR, LF and NUL must be escaped
                        // version 2.0: allow ""@example.com because it's
                        // technically valid
                        this.ResultInfo.Add(@""", CR, LF and NUL must be escaped");
                        return false;
                    }
                }
                else
                {
                    // Unquoted string tests:
                    //
                    // Period (".") may...appear, but may not be used to start or
                    // end the
                    // local part, nor may two or more consecutive periods appear.
                    // (http://tools.ietf.org/html/rfc3696#section-3)
                    //
                    // A zero-length element implies a period at the beginning or
                    // end of the
                    // local part, or two periods together. Either way it's not
                    // allowed.
                    if (string.IsNullOrEmpty(workingElement))
                    {
                        // Dots in wrong place
                        this.ResultInfo.Add(@"
                                        A zero-length element implies a period at the beginning or
                                        end of the local part, or two periods together. Either way it's not
                                        allowed.
                    ");
                        return false;
                    }

                    // Any ASCII graphic (printing) character other than the
                    // at-sign ("@"), backslash, double quote, comma, or square
                    // brackets may
                    // appear without quoting. If any of that list of excluded
                    // characters
                    // are to appear, they must be quoted
                    // (http://tools.ietf.org/html/rfc3696#section-3)
                    //
                    // Any excluded characters? i.e. 0x00-0x20, (, ), <, >, [, ], :,
                    // ;, @, \, comma, period, "
                    if (_regexQuot3.Matches(workingElement).Count > 0)
                    {
                        // These characters must be in a quoted string
                        this.ResultInfo.Add(@"
                                         Any ASCII graphic (printing) character other than the
                                         at-sign (""@""), backslash, double quote, comma, or square
                                         brackets may appear without quoting. If any of that list of excluded
                                         characters are to appear, they must be quoted
                                         (http://tools.ietf.org/html/rfc3696#section-3)
                        ");
                        return false;
                    }

                    //Regex quot4Regex = new Regex("^\\w+");
                    //if (quot4Regex.Matches(working_element).Count == 0)
                    //{
                    //    // First character is an odd one
                    //    return_status = IsEMailResult.ISEMAIL_UNLIKELYINITIAL;
                    //}
                }
            }

            #endregion foreach block

            if (partLength > 64)
            {
                // Local part must be 64 characters or less
                this.ResultInfo.Add(@"Local part must be 64 characters or less");
                return false;
            }

            // Now let's check the domain part...

            // The domain name can also be replaced by an IP address in square
            // brackets
            // (http://tools.ietf.org/html/rfc3696#section-3)
            // (http://tools.ietf.org/html/rfc5321#section-4.1.3)
            // (http://tools.ietf.org/html/rfc4291#section-2.2)
            //
            // IPv4 is the default format for address literals. Alternative formats
            // can
            // be defined. At the time of writing only IPv6 has been defined as an
            // alternative format. Non-IPv4 formats must be tagged to show what type
            // of address literal they are. The registry of current tags is here:
            // http://www.iana.org/assignments/address-literal-tags

            if (_regexAddressLiteral.Matches(domain).Count == 1)
            {
                //// It's an address-literal
                //// Quoted string is unlikely in the real world
                //return_status = IsEMailResult.ISEMAIL_ADDRESSLITERAL;

                // version 2.0: Warning condition added
                String addressLiteral = domain.Substring(1, domain.Length - 2);

                String ipv6;
                int groupMax = 8;

                // revision 2.1: new IPv6 testing strategy

                String colon = ":"; // Revision 2.7: Daniel Marschall's new

                // IPv6 testing strategy
                String doubleColon = "::";

                String ipv6Tag = "IPv6:";

                // Extract IPv4 part from the end of the address-literal (if there is one)

                MatchCollection matchesIP1 = _regexSplitAddress.Matches(addressLiteral);

                if (matchesIP1.Count > 0)
                {
                    int index = addressLiteral.LastIndexOf(matchesIP1[0].Value);

                    if (index == 0)
                    {
                        // Nothing there except a valid IPv4 address, so...
                        return true;
                        // version 2.0: return warning if one is set
                    }
                    else
                    {
                        // - // Assume it's an attempt at a mixed address (IPv6 +
                        // IPv4)
                        // - if ($addressLiteral[$index - 1] !== ':') return
                        // IsEMailResult.ISEMAIL_IPV4BADPREFIX; // Character
                        // preceding IPv4 address must be ':'
                        // revision 2.1: new IPv6 testing strategy
                        if (!addressLiteral.Substring(0, 5).Equals(ipv6Tag))
                        {
                            // RFC5321 section 4.1.3
                            this.ResultInfo.Add(@"Character preceding IPv4 address must be ':' (RFC5321 section 4.1.3)");
                            return false;
                        }

                        // -
                        // - $IPv6 = substr($addressLiteral, 5, ($index === 7) ? 2 :
                        // $index - 6);
                        // - $groupMax = 6;
                        // revision 2.1: new IPv6 testing strategy
                        ipv6 = addressLiteral.Substring(5, index - 5) + "0000:0000"; // Convert IPv4 part to IPv6 format
                    }
                }
                else
                {
                    // It must be an attempt at pure IPv6
                    if (!addressLiteral.Substring(0, 5).Equals(ipv6Tag))
                    {
                        // RFC5321 section 4.1.3
                        this.ResultInfo.Add(@"Invalid IPV6 address (RFC5321 section 4.1.3)");
                        return false;
                    }
                    ipv6 = addressLiteral.Substring(5);

                    // - $groupMax = 8;
                    // revision 2.1: new IPv6 testing strategy
                }

                // Revision 2.7: Daniel Marschall's new IPv6 testing strategy
                Regex split2Regex = new Regex(colon);
                string[] matchesIP = split2Regex.Split(ipv6);
                int groupCount = matchesIP.Length;
                int currIndex = ipv6.IndexOf(doubleColon);

                if (currIndex == -1)
                {
                    // We need exactly the right number of groups
                    if (groupCount != groupMax)
                    {
                        // RFC5321 section 4.1.3
                        this.ResultInfo.Add(@"Invalid IPV6 group count (RFC5321 section 4.1.3)");
                        return false;
                    }
                }
                else
                {
                    if (currIndex != ipv6.LastIndexOf(doubleColon))
                    {
                        // More than one '::'
                        this.ResultInfo.Add(@"IPV6 double double colon present (RFC5321 section 4.1.3)");
                        return false;
                    }
                    if ((currIndex == 0) || (currIndex == (ipv6.Length - 2)))
                    {
                        groupMax++; // RFC 4291 allows :: at the start or end of an
                    }

                    // address with 7 other groups in addition
                    if (groupCount > groupMax)
                    {
                        // Too many IPv6 groups in address
                        this.ResultInfo.Add(@"Too many groups in section (RFC5321 section 4.1.3)");
                        return false;
                    }
                    if (groupCount == groupMax)
                    {
                        // Eliding a single group with :: is deprecated by RFCs 5321 & 5952
                        // & 5952
                        this.ResultInfo.Add(@"Eliding a single group with :: is deprecated by RFCs 5321 & 5952");
                    }
                }

                // Check for single : at start and end of address
                // Revision 2.7: Daniel Marschall's new IPv6 testing strategy
                if (ipv6.StartsWith(colon) && (!ipv6.StartsWith(doubleColon)))
                {
                    // Address starts with a single colon
                    this.ResultInfo.Add(@"IPV6 must start with a single colon (RFC5321 section 4.1.3)");
                    return false;
                }
                if (ipv6.EndsWith(colon) && (!ipv6.EndsWith(doubleColon)))
                {
                    // Address ends with a single colon
                    this.ResultInfo.Add(@"IPV6 must end with a single colon (RFC5321 section 4.1.3)");
                    return false;
                }

                // Check for unmatched characters
                foreach (String s in matchesIP)
                {
                    if (_regexGoodStuff.Matches(s).Count == 0)
                    {
                        this.ResultInfo.Add(@"IPV6 address contains bad characters (RFC5321 section 4.1.3)");
                        return false;
                    }
                }

                // It's a valid IPv6 address, so...
                return true;
                // revision 2.1: bug fix: now correctly return warning status
            }
            else
            {
                // It's a domain name...

                // The syntax of a legal Internet host name was specified in RFC-952
                // One aspect of host name syntax is hereby changed: the
                // restriction on the first character is relaxed to allow either a
                // letter or a digit.
                // (http://tools.ietf.org/html/rfc1123#section-2.1)
                //
                // NB RFC 1123 updates RFC 1035, but this is not currently apparent
                // from reading RFC 1035.
                //
                // Most common applications, including email and the Web, will
                // generally not
                // permit...escaped strings
                // (http://tools.ietf.org/html/rfc3696#section-2)
                //
                // the better strategy has now become to make the
                // "at least one period" test,
                // to verify LDH conformance (including verification that the
                // apparent TLD name
                // is not all-numeric)
                // (http://tools.ietf.org/html/rfc3696#section-2)
                //
                // Characters outside the set of alphabetic characters, digits, and
                // hyphen MUST NOT appear in domain name
                // labels for SMTP clients or servers
                // (http://tools.ietf.org/html/rfc5321#section-4.1.2)
                //
                // RFC5321 precludes the use of a trailing dot in a domain name for
                // SMTP purposes
                // (http://tools.ietf.org/html/rfc5321#section-4.1.2)

                dotArray = _regexSplit.Split(domain);
                partLength = 0;

                // Since we use 'element' after the for each
                // loop let's make sure it has a value
                String lastElement = "";

                // revision 1.13: Line above added because PHPLint now checks for
                // Definitely Assigned Variables

                if (dotArray.Length == 1)
                {
                    this.ResultInfo.Add(@"The mail host probably isn't a TLD");
                }

                // version 2.0: downgraded to a warning

                foreach (String element in dotArray)
                {
                    string workingElement = element;
                    lastElement = element;

                    // Remove any leading or trailing FWS
                    Regex newReg = new Regex("^" + foldingWhiteSpace + "|" + foldingWhiteSpace + "$");
                    String newElement = newReg.Replace(workingElement, string.Empty);

                    if (!element.Equals(newElement))
                    {
                        this.ResultInfo.Add(@"FWS is unlikely in the real world");
                    }

                    workingElement = newElement;

                    // version 2.0: Warning condition added
                    int elementLength = workingElement.Length;

                    // Each dot-delimited component must be of type atext
                    // A zero-length element implies a period at the beginning or
                    // end of the
                    // local part, or two periods together. Either way it's not
                    // allowed.
                    if (elementLength == 0)
                    {
                        // Dots in wrong place
                        this.ResultInfo.Add(@"
                                 Each dot-delimited component must be of type atext
                                 A zero-length element implies a period at the beginning or
                                 end of the
                                 local part, or two periods together. Either way it's not
                                 allowed.
                        ");
                        return false;
                    }

                    // revision 1.15: Speed up the test and get rid of
                    // "uninitialized string offset" notices from PHP

                    // Then we need to remove all valid comments (i.e. those at the
                    // start or end of the element
                    if (workingElement.Substring(0, 1) == "(")
                    {
                        this.ResultInfo.Add(@"Comments are unlikely in the real world");

                        // version 2.0: Warning condition added
                        int indexBrace = workingElement.IndexOf(")");

                        if (indexBrace != -1)
                        {
                            if (_regexComment1.Matches(workingElement.Substring(1, indexBrace - 1)).Count > 0)
                            {
                                // revision 1.17: Fixed name of constant (also
                                // spotted by turbo flash - thanks!)
                                // Illegal characters in comment
                                this.ResultInfo.Add(@"Illegal characters in comment");
                                return false;
                            }
                            workingElement = workingElement.Substring(indexBrace + 1, elementLength - indexBrace - 1);
                            elementLength = workingElement.Length;
                        }
                    }

                    if (!string.IsNullOrEmpty(workingElement) && workingElement.Substring(elementLength - 1, 1) == ")")
                    {
                        // Comments are unlikely in the real world
                        // return_status = IsEMailResult.ISEMAIL_COMMENTS;

                        // version 2.0: Warning condition added
                        int indexBrace = workingElement.LastIndexOf("(");
                        if (indexBrace != -1)
                        {
                            if (_regexComment.Matches(workingElement.Substring(indexBrace + 1, elementLength - indexBrace - 2)).Count > 0)
                            {
                                // revision 1.17: Fixed name of constant (also
                                // spotted by turbo flash - thanks!)
                                // Illegal characters in comment
                                this.ResultInfo.Add(@"Illegal characters in comment");
                                return false;
                            }

                            workingElement = workingElement.Substring(0, indexBrace);
                            elementLength = workingElement.Length;
                        }
                    }

                    // Remove any leading or trailing FWS around the element (inside
                    // any comments)
                    Regex repRegex = new Regex("^" + foldingWhiteSpace + "|" + foldingWhiteSpace + "$");
                    newElement = repRegex.Replace(workingElement, string.Empty);

                    //if (!element.equals(new_element))
                    //{
                    //    // FWS is unlikely in the real world
                    //    return_status = IsEMailResult.ISEMAIL_FWS;
                    //}
                    workingElement = newElement;

                    // version 2.0: Warning condition added

                    // What's left counts towards the maximum length for this part
                    if (partLength > 0)
                    {
                        partLength++; // for the dot
                    }

                    partLength += workingElement.Length;

                    // The DNS defines domain name syntax very generally -- a
                    // string of labels each containing up to 63 8-bit octets,
                    // separated by dots, and with a maximum total of 255
                    // octets.
                    // (http://tools.ietf.org/html/rfc1123#section-6.1.3.5)
                    if (elementLength > 63)
                    {
                        // Label must be 63 characters or less
                        this.ResultInfo.Add(@"
                                 The DNS defines domain name syntax very generally -- a
                                 string of labels each containing up to 63 8-bit octets,
                                 separated by dots, and with a maximum total of 255
                                 octets.
                                 (http://tools.ietf.org/html/rfc1123#section-6.1.3.5)
                        ");
                        return false;
                    }

                    // Any ASCII graphic (printing) character other than the
                    // at-sign ("@"), backslash, double quote, comma, or square
                    // brackets may
                    // appear without quoting. If any of that list of excluded
                    // characters
                    // are to appear, they must be quoted
                    // (http://tools.ietf.org/html/rfc3696#section-3)
                    //
                    // If the hyphen is used, it is not permitted to appear at
                    // either the beginning or end of a label.
                    // (http://tools.ietf.org/html/rfc3696#section-2)
                    //
                    // Any excluded characters? i.e. 0x00-0x20, (, ), <, >, [, ], :,
                    // ;, @, \, comma, period, "

                    if (_regexBadChars.Matches(workingElement).Count > 0)
                    {
                        // Illegal character in domain name
                        this.ResultInfo.Add(@"Illegal character in domain name");
                        return false;
                    }
                }

                if (partLength > 255)
                {
                    // Domain part must be 255 characters or less
                    // (http://tools.ietf.org/html/rfc1123#section-6.1.3.5)
                    this.ResultInfo.Add(@"Domain part must be 255 characters or less (http://tools.ietf.org/html/rfc1123#section-6.1.3.5)");

                    return false;
                }

                if (_regex0To9.Matches(lastElement).Count > 0)
                {
                    this.ResultInfo.Add(@"TLD probably isn't all-numeric (http://www.apps.ietf.org/rfc/rfc3696.html#sec-2)");
                    // version 2.0: Downgraded to a warning
                }
            }

            if (_regexSpamHouses.IsMatch(email))
                return false;

            // Eliminate all other factors, and the one which remains must be the
            // truth. (Sherlock Holmes, The Sign of Four)
            return true;
        }

        /*
         * Replaces a char in a String
         *
         * @param s
         *            The input string
         * @param pos
         *            The position of the char to be replaced
         * @param c
         *            The new char
         * @return The new String
         * @see Source: http://www.rgagnon.com/javadetails/java-0030.html
         */

        private static String ReplaceCharAt(String s, int pos, char c)
        {
            return s.Substring(0, pos) + c + s.Substring(pos + 1);
        }
    }
}

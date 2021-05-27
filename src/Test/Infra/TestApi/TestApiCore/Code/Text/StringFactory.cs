// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;


namespace Microsoft.Test.Text
{
    /// <summary>
    /// Provides factory methods for generation of text, interesting from the testing point of view.
    /// </summary>
    ///
    /// <example>
    /// The following example demonstrates how to generate two equivalent strings 
    /// of a fixed length (20 Unicode points), with numbers.
    /// A Unicode code point may correspond to 1 or 2 characters. For more information see <see cref="StringProperties"/>:
    /// <code>
    /// StringProperties properties = new StringProperties();
    ///
    /// properties.MinNumberOfCodePoints = 20;
    /// properties.MaxNumberOfCodePoints = 20;
    /// properties.HasNumbers = true;
    /// properties.UnicodeRanges.Add(new UnicodeRange(0, 0xFFFF));
    ///
    /// string s1 = StringFactory.GenerateRandomString(properties, 5678);
    /// string s2 = StringFactory.GenerateRandomString(properties, 5678);
    /// </code>
    /// </example>
    public static class StringFactory
    {
        private static readonly UnicodeRangeDatabase database = new UnicodeRangeDatabase();
        private static PropertyFactory propertyFactory = null;
        private static StringProperties properties = null;
        private static StringProperties cachedProperties = null;
        private static Collection<UnicodeRange> ranges = new Collection<UnicodeRange>();
        private static int minNumCodePoints = 0;
        private static int maxNumCodePoints = 0;
        private static int numOfCodePoints = 0;
        
        private static readonly List<UnicodeRange> alphabetRangeList = new List<UnicodeRange>();

        /// <summary>
        /// Returns a string, conforming to the <see cref="StringProperties"/> provided.
        /// </summary>
        /// <param name="stringProperties">The properties of the strings to be generated by the factory.</param>
        /// <param name="seed">The random number generator seed.</param>
        /// <returns>A string, conforming to the previously specified properties.</returns>
        public static string GenerateRandomString(StringProperties stringProperties, int seed)
        {
            if (null == properties || IsPropertyChanged(stringProperties))
            {
                properties = stringProperties;
                // Make a deep copy of stringProperties to cache it. If user changes any property and calls this API again,
                // InitializeProperties() is triggered. Otherwise, no need to re initialize properties for optimization.
                CacheProperties();
                InitializeProperties();
            }
            
            string retStr = string.Empty;
            Random rand = new Random(seed);
            numOfCodePoints = rand.Next(minNumCodePoints, maxNumCodePoints);
            
            int numberOfProperties = propertyFactory.PropertyDictionary.Count;
            if (0 == numberOfProperties)
            {
                for (int i=0; i < numOfCodePoints; i++)
                {
                    retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));
                }
                return retStr;
            }

            int quote = numOfCodePoints / propertyFactory.MinNumOfCodePoint;
            if (0 == quote)
            {
                throw new ArgumentOutOfRangeException(
                    "StringFactory, MinNumberOfCodePoints needs to be at least " + numberOfProperties * propertyFactory.MinNumOfCodePoint + ".");
            }

            Dictionary<PropertyFactory.PropertyName, IStringProperty>.KeyCollection keyColl =  propertyFactory.PropertyDictionary.Keys;
            foreach (PropertyFactory.PropertyName name in keyColl)
            {
                if (PropertyFactory.PropertyName.Bidi == name)
                {
                    retStr += GenerateBidiString(quote * BidiProperty.MINNUMOFCODEPOINT, seed);
                }
                else if (PropertyFactory.PropertyName.CombiningMarks == name)
                {
                    retStr += GenerateCombiningMarkString(quote * CombiningMarksProperty.MINNUMOFCODEPOINT * (int)properties.MinNumberOfCombiningMarks, seed);
                }
                else if (PropertyFactory.PropertyName.Eudc == name)
                {
                    retStr += GenerateStringWithEudc(quote * EudcProperty.MINNUMOFCODEPOINT * (int)properties.MinNumberOfEndUserDefinedCodePoints, seed);
                }
                else if (PropertyFactory.PropertyName.LineBreak == name)
                {
                    retStr += GenerateStringWithLineBreak(quote * LineBreakProperty.MINNUMOFCODEPOINT * (int)properties.MinNumberOfLineBreaks, seed);
                }
                else if (PropertyFactory.PropertyName.Number == name)
                {
                    retStr += GenerateStringWithNumber(quote * NumberProperty.MINNUMOFCODEPOINT, seed);
                }
                else if (PropertyFactory.PropertyName.Surrogate == name)
                {
                    retStr += GenerateStringWithSurrogatePair(quote * SurrogatePairProperty.MINNUMOFCODEPOINT * (int)properties.MinNumberOfSurrogatePairs, seed);
                }
                else if (PropertyFactory.PropertyName.TextNormalization == name)
                {
                    retStr += GenerateNormalizedString(quote * TextNormalizationProperty.MINNUMOFCODEPOINT, seed);
                }
                else if (PropertyFactory.PropertyName.TextSegmentation == name)
                {
                    retStr += GenerateStringWithSegmentation(
                        quote * TextSegmentationProperty.MINNUMOFCODEPOINT * (int)properties.MinNumberOfTextSegmentationCodePoints,
                        seed);
                }
            }

            if (numOfCodePoints > 0)
            {
                for (int i=0; i < numOfCodePoints; i++)
                {
                    retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));
                }
            }

            if (null != properties.NormalizationForm)
            {
                retStr = retStr.Normalize((NormalizationForm)properties.NormalizationForm);
            }

            return retStr;
        }

        private static void CacheProperties()
        {
            if (null == cachedProperties)
            {
                cachedProperties = new StringProperties();
            }

            cachedProperties.UnicodeRanges.Clear();
            if (0 != properties.UnicodeRanges.Count)
            {
                foreach (UnicodeRange range in properties.UnicodeRanges)
                {
                    cachedProperties.UnicodeRanges.Add(range);
                }
            }

            cachedProperties.HasNumbers = properties.HasNumbers;
            cachedProperties.IsBidirectional = properties.IsBidirectional;
            cachedProperties.NormalizationForm = properties.NormalizationForm;
            cachedProperties.MinNumberOfCombiningMarks = properties.MinNumberOfCombiningMarks;
            cachedProperties.MinNumberOfCodePoints = properties.MinNumberOfCodePoints;
            cachedProperties.MaxNumberOfCodePoints = properties.MaxNumberOfCodePoints;
            cachedProperties.MinNumberOfEndUserDefinedCodePoints = properties.MinNumberOfEndUserDefinedCodePoints;
            cachedProperties.MinNumberOfLineBreaks = properties.MinNumberOfLineBreaks;
            cachedProperties.MinNumberOfSurrogatePairs = properties.MinNumberOfSurrogatePairs;
            cachedProperties.MinNumberOfTextSegmentationCodePoints = properties.MinNumberOfTextSegmentationCodePoints;
        }

        private static bool IsPropertyChanged(StringProperties stringProperties)
        {
            if ((0 == cachedProperties.UnicodeRanges.Count && 0 != stringProperties.UnicodeRanges.Count) ||
                (0 != cachedProperties.UnicodeRanges.Count && 0 == stringProperties.UnicodeRanges.Count))
            {
                return true;
            }
            else if (0 != cachedProperties.UnicodeRanges.Count && 0 != stringProperties.UnicodeRanges.Count)
            {
                if (cachedProperties.UnicodeRanges.Count != stringProperties.UnicodeRanges.Count)
                {
                    return true;
                }

                int i = 0;
                foreach (UnicodeRange range in cachedProperties.UnicodeRanges)
                {
                    if (range.StartOfUnicodeRange != stringProperties.UnicodeRanges[i].StartOfUnicodeRange)
                    {
                        return true;
                    }

                    if (range.EndOfUnicodeRange != stringProperties.UnicodeRanges[i++].EndOfUnicodeRange)
                    {
                        return true;
                    }
                }
            }

            if (cachedProperties.HasNumbers != stringProperties.HasNumbers)
            {
                return true;
            }

            if (cachedProperties.IsBidirectional != stringProperties.IsBidirectional)
            {
                return true;
            }

            if (cachedProperties.NormalizationForm != stringProperties.NormalizationForm)
            {
                return true;
            }

            if (cachedProperties.MinNumberOfCombiningMarks != stringProperties.MinNumberOfCombiningMarks)
            {
                return true;
            }

            if (cachedProperties.MinNumberOfCodePoints != stringProperties.MinNumberOfCodePoints)
            {
                return true;
            }

            if (cachedProperties.MaxNumberOfCodePoints != stringProperties.MaxNumberOfCodePoints)
            {
                return true;
            }

            if (cachedProperties.MinNumberOfEndUserDefinedCodePoints != stringProperties.MinNumberOfEndUserDefinedCodePoints)
            {
                return true;
            }

            if (cachedProperties.MinNumberOfLineBreaks != stringProperties.MinNumberOfLineBreaks)
            {
                return true;
            }

            if (cachedProperties.MinNumberOfSurrogatePairs != stringProperties.MinNumberOfSurrogatePairs)
            {
                return true;
            }

            if (cachedProperties.MinNumberOfTextSegmentationCodePoints != stringProperties.MinNumberOfTextSegmentationCodePoints)
            {
                return true;
            }

            return false;
        }

        private static void InitializeProperties()
        {
            if (0 != properties.UnicodeRanges.Count)
            {
                ranges.Clear();
                foreach (UnicodeRange range in properties.UnicodeRanges)
                {
                    ranges.Add(range);
                }
            }
            else
            {
                ranges.Add(new UnicodeRange(0, TextUtil.MaxUnicodePoint));
            }

            // Validation for Unicode ranges provided against each property is done when each property is created
            propertyFactory = new PropertyFactory(properties, database, ranges);

            // Combining mark property needs Latin alphabet
            if (propertyFactory.HasProperty(PropertyFactory.PropertyName.CombiningMarks))
            {
                InitializeAlphabetRangeList();
            }
            
            // Get minimum number of points
            minNumCodePoints = propertyFactory.MinNumOfCodePoint;
            
            if (null == properties.MinNumberOfCodePoints && null == properties.MaxNumberOfCodePoints)
            {
                maxNumCodePoints = TextUtil.MAXNUMOFCODEPOINT;
                if (minNumCodePoints > maxNumCodePoints)
                {
                    throw new ArgumentOutOfRangeException(
                        "StringFactory, maximum number of code points is greater than maximum allowed " + maxNumCodePoints + ".");
                }
            }
            else if (null != properties.MinNumberOfCodePoints && null == properties.MaxNumberOfCodePoints)
            {
                minNumCodePoints = (int)properties.MinNumberOfCodePoints;
                if (minNumCodePoints > TextUtil.MAXNUMOFCODEPOINT)
                {
                    throw new ArgumentOutOfRangeException(
                        "StringFactory, maximum number of code points allowed is " + TextUtil.MAXNUMOFCODEPOINT + ".");
                }
                maxNumCodePoints = TextUtil.MAXNUMOFCODEPOINT;
            }
            else if (null == properties.MinNumberOfCodePoints && null != properties.MaxNumberOfCodePoints)
            {
                maxNumCodePoints = (int)properties.MaxNumberOfCodePoints;
                if (maxNumCodePoints < propertyFactory.MinNumOfCodePoint)
                {
                    throw new ArgumentOutOfRangeException(
                        "StringFactory, minimum number of code points needed is " + propertyFactory.MinNumOfCodePoint + ".");
                }
            }
            else
            {
                minNumCodePoints = (int)properties.MinNumberOfCodePoints;
                maxNumCodePoints = (int)properties.MaxNumberOfCodePoints;
                if (minNumCodePoints > maxNumCodePoints)
                {
                    throw new ArgumentOutOfRangeException("StringFactory, MinNumberOfCodePoints, " + minNumCodePoints + " cannot be bigger than " +
                        "MaxNumberOfCodePoints, " + maxNumCodePoints + ".");
                }
            }
        }

        /// <summary>
        /// Get next code point
        /// </summary>
        private static int GetNextCodePoint(Random rand, int seed)
        {
            int ctr = 0;
            int index = rand.Next(0, ranges.Count);
            int next = rand.Next(ranges[index].StartOfUnicodeRange, ranges[index].EndOfUnicodeRange);
            while ((next >= 0xD800 && next <= 0xDBFF) || (next >= 0xDC00 && next <= 0xDFFF))
            {
                if (propertyFactory.HasProperty(PropertyFactory.PropertyName.Surrogate))
                {
                    return SurrogatePairStringToInt(
                        (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.Surrogate]).GetRandomCodePoints(1, rand.Next()));
                }
                next = rand.Next(ranges[index].StartOfUnicodeRange, ranges[index].EndOfUnicodeRange);
                ctr++;
                if (TextUtil.MAXNUMITERATION == ctr)
                {
                    throw new ArgumentOutOfRangeException(
                        "StringFactory, " + ctr + " loop reached." + "GetNextCodePoint aren't able to get code point beyond surrogate pair range. " +
                        "Check UnicodeChart range.");
                }
            }

            return next;
        }

        /// <summary>
        /// Convert a pair of Surrogate from string to UTF32
        /// </summary>
        private static int SurrogatePairStringToInt(string pair)
        {
            int high = Convert.ToInt32(pair[0]);
            int low  = Convert.ToInt32(pair[1]);
            return (high - 0xD800) * 0x400 + (low - 0xDC00) + 0x10000;
        }

        /// <summary>
        /// Construct bidi string
        /// </summary>
        private static string GenerateBidiString(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code point to construct bidi string.");
            }
            
            if (numOfPropertyCodePoints < BidiProperty.MINNUMOFCODEPOINT)
            {
                throw new ArgumentOutOfRangeException(
                    "StringFactory, minimum bidi string needs " + BidiProperty.MINNUMOFCODEPOINT + " code points.");
            }
            
            Random rand = new Random(seed);
            // Note, numOfBidi is number of property - 1 numOfBidi is 2 code points
            int numOfBidi = rand.Next(1, numOfPropertyCodePoints / BidiProperty.MINNUMOFCODEPOINT);
            int left = numOfPropertyCodePoints - numOfBidi * BidiProperty.MINNUMOFCODEPOINT;

            string bidiStr = string.Empty;
            bidiStr += (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.Bidi]).GetRandomCodePoints(numOfBidi, seed);
            if (left <= 0)
            {
                return bidiStr;
            }

            string retStr = string.Empty;
            int temp = 0;
            
            // Not using TextUtil.GetRandomCodePoint for more randomness
            for (int i=1; i <= left; i++)
            {
                temp = GetNextCodePoint(rand, seed);
                retStr += TextUtil.IntToString(temp);
            }
            retStr += bidiStr;

            return retStr;
        }
        
        /// <summary>
        /// Construct string with combining marks
        /// </summary>
        private static string GenerateCombiningMarkString(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code point to construct string with combining mark.");
            }
            
            // MinNumberOfCombiningMarks is null or not should have been checked
            int numOfCombiningMarks = (int)properties.MinNumberOfCombiningMarks;

            if (numOfPropertyCodePoints < numOfCombiningMarks * CombiningMarksProperty.MINNUMOFCODEPOINT)
            {
                throw new ArgumentOutOfRangeException(
                    "StringFactory, minimum string cotains combining mark needs " + 
                    numOfCombiningMarks * CombiningMarksProperty.MINNUMOFCODEPOINT + " code points.");
            }
            
            // Construct combining marks string
            Random rand = new Random(seed);
            // Half Latin half combining symbols
            int numOfLatin = numOfCombiningMarks;
            int left = numOfPropertyCodePoints - numOfCombiningMarks * CombiningMarksProperty.MINNUMOFCODEPOINT;
            
            string latinStr = string.Empty;
            int index = rand.Next(0, alphabetRangeList.Count);
            // From a random range in alphabetRangeList
            latinStr += TextUtil.GetRandomCodePoint(alphabetRangeList[index], numOfLatin, null, seed);
            
            string combiningMarkStr = string.Empty;
            combiningMarkStr += 
                (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.CombiningMarks]).GetRandomCodePoints(numOfCombiningMarks, seed);

            string retStr = string.Empty;
            int i = 0;
            foreach (char next in latinStr)
            {
                retStr += next;
                retStr += combiningMarkStr[i++];
            }

            // Leftover
            for (i = 0; i < left; i++)
            {
                retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));
            }

            return retStr;
        }

        /// <summary>
        /// Construct string with EUDC
        /// </summary>
        private static string GenerateStringWithEudc(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code point to construct string with EUDC.");
            }
            
             // MinNumberOfEndUserDefinedCodePoints of EUDC is null or not should have been checked
            int numOfEudc = (int)properties.MinNumberOfEndUserDefinedCodePoints;

            int left = numOfPropertyCodePoints - (numOfEudc * EudcProperty.MINNUMOFCODEPOINT);

            string eudcStr = string.Empty;
            eudcStr += (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.Eudc]).GetRandomCodePoints(numOfEudc, seed);
            if (left <= 0)
            {
                return eudcStr;
            }

            string retStr = string.Empty;
            Random rand = new Random(seed);
            // numOfEudc is 0 or not is checked in PropertiesFactory
            int quote = left / numOfEudc ;
            

            int j = 0;
            for (int i=1; i <= left; i++)
            {
                retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));

                if (quote > 0 && j < eudcStr.Length)
                {
                    if (i % quote == 0)
                    {
                        retStr += eudcStr[j++];
                    }
                }
            }

            for (int i=j; i < eudcStr.Length; i++)
            {
                retStr += eudcStr[i];
            }

            return retStr;
        }


        /// <summary>
        /// Construct string with Linebreaks
        /// </summary>
        private static string GenerateStringWithLineBreak(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code point to construct string with line break.");
            }

            // MinNumberOfLineBreaks is null or not should have been checked
            int numOfLineBreaks = (int)properties.MinNumberOfLineBreaks;

            int left = numOfPropertyCodePoints - numOfLineBreaks * LineBreakProperty.MINNUMOFCODEPOINT;

            string lineBreakStr = string.Empty;
            lineBreakStr += (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.LineBreak]).GetRandomCodePoints(numOfLineBreaks, seed);
            if (left <= 0)
            {
                return lineBreakStr;
            }

            string retStr = string.Empty;
            Random rand = new Random(seed);
            int quote = left / numOfLineBreaks;
            
            int j = 0;
            for (int i=1; i <= left; i++)
            {
                retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));

                if (quote > 0 && j < lineBreakStr.Length)
                {
                    if (0 == i % quote)
                    {
                        retStr += lineBreakStr[j++];
                    }
                }
            }

            for (int i=j; i < lineBreakStr.Length; i++)
            {
                retStr += lineBreakStr[i];
            }

            return retStr;
        }

        /// <summary>
        /// Construct string with numbers
        /// </summary>
        private static string GenerateStringWithNumber(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code point to construct string with number.");
            }
            
            int numOfNumbers = 0;
            Random rand = new Random(seed);
            numOfNumbers = rand.Next(1,  numOfPropertyCodePoints / NumberProperty.MINNUMOFCODEPOINT);
            
            int left = numOfPropertyCodePoints - numOfNumbers * NumberProperty.MINNUMOFCODEPOINT;

            string numStr = string.Empty;
            numStr += (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.Number]).GetRandomCodePoints(numOfNumbers, seed);
            if (left <= 0)
            {
                return numStr;
            }

            int quote = left / numOfNumbers;
            string retStr = string.Empty;

            int j = 0;
            for (int i=1; i <= left; i++)
            {
                retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));

                if (quote > 0 && j < numStr.Length)
                {
                    if (0 == i % quote)
                    {
                        retStr += numStr[j++];
                    }
                }
            }

            for (int i=j; i < numStr.Length; i++)
            {
                retStr += numStr[i];
            }

            return retStr;
        }

        /// <summary>
        /// Construct string with Surrogate pairs
        /// </summary>
        private static string GenerateStringWithSurrogatePair(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code points for surrogate pair string.");
            }

            int numOfSurrogatePairs = (int)properties.MinNumberOfSurrogatePairs;
            if (numOfPropertyCodePoints < numOfSurrogatePairs * SurrogatePairProperty.MINNUMOFCODEPOINT)
            {
                throw new ArgumentOutOfRangeException(
                    "StringFactory, minimum string cotains surrogate pair needs " + 
                    numOfSurrogatePairs * SurrogatePairProperty.MINNUMOFCODEPOINT + " code points.");
            }

            string surrogatePairStr = string.Empty;
            surrogatePairStr += (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.Surrogate]).GetRandomCodePoints(numOfSurrogatePairs, seed);
            int left = numOfPropertyCodePoints - numOfSurrogatePairs * SurrogatePairProperty.MINNUMOFCODEPOINT;
            if (left <= 0)
            {
                return surrogatePairStr;
            }

            string retStr = string.Empty;
            int quote = left / numOfSurrogatePairs;
            Random rand = new Random(seed);

            int j = 0;
            for (int i=1; i <= left; i++)
            {
                retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));

                if (quote > 0 && j < surrogatePairStr.Length)
                {
                    if (0 == i % quote)
                    {
                        retStr += surrogatePairStr[j];
                        retStr += surrogatePairStr[j+1];
                        j += 2;
                    }
                }
            }

            for (int i=j; i < surrogatePairStr.Length; i++)
            {
                retStr += surrogatePairStr[i];
            }

            return retStr;
        }

        /// <summary>
        /// Construct normalized text string
        /// </summary>
        private static string GenerateNormalizedString(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code points for normalized string.");
            }

            Random rand = new Random(seed);
            int numOfNormalizationCodePoint = rand.Next(1,  numOfPropertyCodePoints / TextNormalizationProperty.MINNUMOFCODEPOINT);
            int left = numOfPropertyCodePoints - numOfNormalizationCodePoint * TextNormalizationProperty.MINNUMOFCODEPOINT;

            string normalizedStr = string.Empty;
            normalizedStr += (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.TextNormalization]).GetRandomCodePoints(
                numOfNormalizationCodePoint,
                seed);
            if (left <= 0)
            {
                return normalizedStr;
            }

            string retStr = string.Empty;
            int quote = left / numOfNormalizationCodePoint;

            int j = 0;
            for (int i=1; i <= left; i++)
            {
                retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));

                if (quote > 0 && j < normalizedStr.Length)
                {
                    if (0 == i % quote)
                    {
                        retStr += normalizedStr[j];
                        if (normalizedStr[j] >= 0xD800 && normalizedStr[j] <= 0xDBFF)
                        {
                            retStr += normalizedStr[j+1];
                            j++;
                        }
                        j++;
                    }
                }
            }

            for (int i=j; i < normalizedStr.Length; i++)
            {
                retStr += normalizedStr[i];
            }

            return retStr;
        }

        /// <summary>
        /// Construct string with segmentation code point
        /// </summary>
        private static string GenerateStringWithSegmentation(int numOfPropertyCodePoints, int seed)
        {
            if (numOfPropertyCodePoints < 1)
            {
                throw new ArgumentOutOfRangeException("StringFactory, numOfPropertyCodePoints, " + numOfPropertyCodePoints + " cannot be less than one.");
            }
            numOfCodePoints -= numOfPropertyCodePoints;
            
            if (numOfCodePoints < 0)
            {
                throw new ArgumentOutOfRangeException("StringFactory, " + numOfCodePoints + 
                    "left for the operation. Not enough code point to segementation.");
            }

            // MinNumberOfTextSegmentationCodePoints is null or not should have been checked
            int numOfTextSegmentationChars = (int)properties.MinNumberOfTextSegmentationCodePoints;
            int left = numOfPropertyCodePoints - numOfTextSegmentationChars * TextSegmentationProperty.MINNUMOFCODEPOINT;

            string segmentationStr = string.Empty;
            segmentationStr += (propertyFactory.PropertyDictionary[PropertyFactory.PropertyName.TextSegmentation]).GetRandomCodePoints(
                numOfTextSegmentationChars,
                seed);
            if (left <= 0)
            {
                return segmentationStr;
            }

            string retStr = string.Empty;
            Random rand = new Random(seed);
            int quote = left / numOfTextSegmentationChars;
            
            int j = 0;
            for (int i=1; i <= left; i++)
            {
                retStr += TextUtil.IntToString(GetNextCodePoint(rand, seed));

                if (quote > 0 && j < segmentationStr.Length)
                {
                    if (0 == i % quote)
                    {
                        retStr += segmentationStr[j++];
                    }
                }
            }

            for (int i=j; i < segmentationStr.Length; i++)
            {
                retStr += segmentationStr[i];
            }

            return retStr;
        }

        private static void InitializeAlphabetRangeList( )
        {
            bool isValid = false;

            foreach (UnicodeRange range in ranges)
            {            
                UnicodeRange newRange = RangePropertyCollector.GetRange(new UnicodeRange(0x0041, 0x005A), range);
                if (null != newRange)
                {
                    alphabetRangeList.Add(newRange);
                    isValid = true;
                }

                newRange = RangePropertyCollector.GetRange(new UnicodeRange(0x0061, 0x007A), range);
                if (null != newRange)
                {
                    alphabetRangeList.Add(newRange);
                    isValid = true;
                }
            }

            if (!isValid)
            {
                throw new ArgumentOutOfRangeException("StringFactory, Latin alphabet ranges for Combining mark property are beyond expected. " +
                    "Refer to Latin Alphabet range 0x0041 - 0x005A and 0x0061 - 0x007A." + "All " + ranges.Count + " UniCodeRange is in Latin alphabet range");
            }
        }
    }
}
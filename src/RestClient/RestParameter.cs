/// <summary>
/// 
/// The MIT License (MIT)
/// 
/// Copyright (c) 2020 Federico Mazzanti
/// 
/// Permission is hereby granted, free of charge, to any person
/// obtaining a copy of this software and associated documentation
/// files (the "Software"), to deal in the Software without
/// restriction, including without limitation the rights to use,
/// copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the
/// Software is furnished to do so, subject to the following
/// conditions:
/// 
/// The above copyright notice and this permission notice shall be
/// included in all copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
/// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
/// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
/// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
/// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
/// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
/// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
/// OTHER DEALINGS IN THE SOFTWARE.
/// 
/// </summary>

namespace RestClient
{
    /// <summary>
    /// Provides a class the represents the querystring parameter
    /// </summary>
    public struct RestParameter
    {
        /// <summary>
        /// Key parameter
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Value parameter
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// true if the Key has a value; false if the Key has no value.
        /// </summary>
        public bool HasKey => !(Key is null);

        /// <summary>
        /// true if the Value has a value; false if the Value has no value.
        /// </summary>
        public bool HasValue => !(Value is null);

        /// <summary>
        /// true if the Key and Value have value; if either has null value. 
        /// </summary>
        public bool IsValid => HasKey && HasValue;
    }
}

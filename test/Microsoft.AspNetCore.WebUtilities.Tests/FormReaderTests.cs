// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.AspNetCore.WebUtilities
{
    public class FormReaderTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_EmptyKeyAtEndAllowed(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "=bar");

            var formCollection = await ReadFormAsync(new FormReader(body));

            Assert.Equal("bar", formCollection[""].ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_EmptyKeyWithAdditionalEntryAllowed(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "=bar&baz=2");

            var formCollection = await ReadFormAsync(new FormReader(body));

            Assert.Equal("bar", formCollection[""].ToString());
            Assert.Equal("2", formCollection["baz"].ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_EmptyValuedAtEndAllowed(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=");

            var formCollection = await ReadFormAsync(new FormReader(body));

            Assert.Equal("", formCollection["foo"].ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_EmptyValuedWithAdditionalEntryAllowed(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=&baz=2");

            var formCollection = await ReadFormAsync(new FormReader(body));

            Assert.Equal("", formCollection["foo"].ToString());
            Assert.Equal("2", formCollection["baz"].ToString());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_KeyCountLimitMet_Success(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=1&bar=2&baz=3&baz=4");

            var formCollection = await ReadFormAsync(new FormReader(body) { KeyCountLimit = 3 });

            Assert.Equal("1", formCollection["foo"].ToString());
            Assert.Equal("2", formCollection["bar"].ToString());
            Assert.Equal("3,4", formCollection["baz"].ToString());
            Assert.Equal(3, formCollection.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_KeyCountLimitExceeded_Throw(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=1&baz=2&bar=3&baz=4&baf=5");

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ReadFormAsync(new FormReader(body) { KeyCountLimit = 3 }));
            Assert.Equal("Form key count limit 3 exceeded.", exception.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_KeyLengthLimitMet_Success(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=1&bar=2&baz=3&baz=4");

            var formCollection = await ReadFormAsync(new FormReader(body) { KeyLengthLimit = 10 });

            Assert.Equal("1", formCollection["foo"].ToString());
            Assert.Equal("2", formCollection["bar"].ToString());
            Assert.Equal("3,4", formCollection["baz"].ToString());
            Assert.Equal(3, formCollection.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_KeyLengthLimitExceeded_Throw(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=1&baz1234567890=2");

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ReadFormAsync(new FormReader(body) { KeyLengthLimit = 10 }));
            Assert.Equal("Form key or value length limit 10 exceeded.", exception.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_ValueLengthLimitMet_Success(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=1&bar=1234567890&baz=3&baz=4");

            var formCollection = await ReadFormAsync(new FormReader(body) { ValueLengthLimit = 10 });

            Assert.Equal("1", formCollection["foo"].ToString());
            Assert.Equal("1234567890", formCollection["bar"].ToString());
            Assert.Equal("3,4", formCollection["baz"].ToString());
            Assert.Equal(3, formCollection.Count);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadFormAsync_ValueLengthLimitExceeded_Throw(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=1&baz=1234567890123");

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => ReadFormAsync(new FormReader(body) { ValueLengthLimit = 10 }));
            Assert.Equal("Form key or value length limit 10 exceeded.", exception.Message);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadNextPair_ReadsAllPairs(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "foo=&baz=2");

            var reader = new FormReader(body);

            var pair = (KeyValuePair<string, string>)await ReadPair(reader);

            Assert.Equal("foo", pair.Key);
            Assert.Equal("", pair.Value);

            pair = (KeyValuePair<string, string>)await ReadPair(reader);

            Assert.Equal("baz", pair.Key);
            Assert.Equal("2", pair.Value);

            Assert.Null(await ReadPair(reader));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReadNextPair_ReturnsNullOnEmptyStream(bool bufferRequest)
        {
            var body = MakeStream(bufferRequest, "");

            var reader = new FormReader(body);

            Assert.Null(await ReadPair(reader));
        }

        protected virtual Task<Dictionary<string, StringValues>> ReadFormAsync(FormReader reader)
        {
            return Task.FromResult(reader.ReadForm());
        }

        protected virtual Task<KeyValuePair<string, string>?> ReadPair(FormReader reader)
        {
            return Task.FromResult(reader.ReadNextPair());
        }

        private static Stream MakeStream(bool bufferRequest, string text)
        {
            var formContent = Encoding.UTF8.GetBytes(text);
            Stream body = new MemoryStream(formContent);
            if (!bufferRequest)
            {
                body = new NonSeekableReadStream(body);
            }
            return body;
        }
    }
}
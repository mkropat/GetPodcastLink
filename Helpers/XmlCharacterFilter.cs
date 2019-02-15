using System.IO;
using System.Linq;

namespace GetPodcastLink.Helpers {
    public class XmlCharacterFilter : TextReader {
        readonly TextReader _source;

        public XmlCharacterFilter(TextReader source)
        {
            _source = source;
        }

        public override int Peek()
        {
            var c = _source.Peek();

            while (c > 0 && !IsLegalCharacter((char) c))
            {
                _source.Read();
                c = _source.Peek();
            }

            return c;
        }

        public override int Read()
        {
            int c;

            do
            {
                c = _source.Read();
            }
            while (c > 0 && !IsLegalCharacter((char)c));
            
            return c;
        }

        static bool IsLegalCharacter(char c)
        {
            return !(0x0 <= c && c <= 0x8) &&
                !new[] { 0xB, 0xC }.Contains(c) &&
                !(0xE <= c && c <= 0x1F) &&
                !(0x7F <= c && c <= 0x84) &&
                !(0x86 <= c && c <= 0x9F) &&
                !(0xD800 <= c && c <= 0xDFFF) &&
                !new[] { 0xFFFE, 0xFFFF }.Contains(c);
        }
    }
}
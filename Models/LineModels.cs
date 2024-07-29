using System.Collections.Generic;
using System.Linq;

namespace TitsPlay.Models
{
    public class Line{

        public string Speaker { get; set; }
        public List<LineItem> Lines { get; set; } = new List<LineItem>();
        private int cursor = 0;



        public Line(string speaker, string line, string id)
        {
            Speaker = speaker;
            Lines.Add(new LineItem { Line = line, Id = id });
        }

        public void AddLine(string line, string id)
        {
            Lines.Add(new LineItem { Line = line, Id = id });
        }

        public List<string> GetLineIds()
        {
            return Lines.Select(x => x.Id).ToList();
        }

        public bool HasNext()
        {
            return cursor < (Lines.Count - 1);
        }

        public LineItem Next()
        {
            cursor++;
            return Lines[cursor];
        }

        public bool HasPrev()
        {
            return cursor > 0;
        }

        public LineItem Prev()
        {
            cursor--;
            if(HasPrev())return Lines[cursor - 1];
            else{
                return Lines[cursor];
            }
        }

        public LineItem Peek()
        {
            return Lines[cursor];
        }

        public bool isFirst()
        {
            return cursor == 0;
        }

        public bool isLast()
        {
            return cursor == Lines.Count - 1;
        }
    }

    public class LineItem
    {
        public required string Line { get; set; }
        public required string Id { get; set; }
    }
}

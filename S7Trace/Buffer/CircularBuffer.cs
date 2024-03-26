using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace S7Trace.Buffer
{
    public class CircularBuffer<T>
    {
        private readonly T[] buffer;
        private int head;
        private int tail;
        private bool isFull;

        public CircularBuffer(int capacity)
        {
            buffer = new T[capacity];
            head = capacity - 1;
        }

        public void Add(T item)
        {
            head = (head + 1) % buffer.Length;
            buffer[head] = item;
            if (isFull)
            tail = (tail + 1) % buffer.Length;
            else if (head == tail)
            isFull = true;
        }

        public T[] ToArray()
        {
            if (!isFull && head == -1) return new T[0];
            int length = isFull ? buffer.Length : (head >= tail ? head - tail + 1 : buffer.Length - tail + head + 1);
            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            result[i] = buffer[(tail + i) % buffer.Length];
            return result;
        }

        public int Capacity => buffer.Length;
        public bool IsFull => isFull;
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Muse_RP.Message;
using System.Linq;
namespace Muse_RP.Utils
{
    public class SortedMuseBuffer<Element> : IDisposable
    {
        //Storage
        private readonly SortedSet<Element> buffer;
        //Mutex
        private readonly Mutex bufferMutex;
        //Properties
        private int bufferSize;

        #region Constructor
        public SortedMuseBuffer(IComparer<Element> comparer)
        {
            buffer = new SortedSet<Element>(comparer);
            bufferMutex = new Mutex(false);
            bufferSize = 0;
        }
        #endregion
        #region Getters and Setters
        public int getSize() { bufferMutex.WaitOne(); int size = bufferSize; bufferMutex.ReleaseMutex(); return size; }
        #endregion
        #region Dequeue
        /// <summary>
        /// Dequeues the oldest element of the buffer
        /// </summary>
        /// <returns>Oldest element of the buffer</returns>
        public Element Dequeue()
        {
            bufferMutex.WaitOne();
            if (bufferSize > 0)
            {
                Element element = buffer.First();
                buffer.Remove(element);

                bufferSize--;

                bufferMutex.ReleaseMutex();

                return element;

            }
            bufferMutex.ReleaseMutex();
            return default(Element);
        }
        /// <summary>
        /// Gets the first element of the buffer
        /// </summary>
        /// <returns>First element of the buffer</returns>
        public Element GetFirst()
        {
            bufferMutex.WaitOne();
            if (bufferSize > 0)
            {

                Element element = buffer.First();
                bufferMutex.ReleaseMutex();

                return element;

            }
            bufferMutex.ReleaseMutex();
            return default(Element);
        }
        /// <summary>
        /// Dequeues the target times
        /// </summary>
        /// <param name="size">Times to dequeue</param>
        public void Remove(uint size)
        {
            for (uint i = 0; i < size; i++)
            {
                Dequeue();
            }

        }
        #endregion
        #region Enqueue
        /// <summary>
        /// Enqueue a new element
        /// </summary>
        /// <param name="element">Element to add</param>
        public void Add(Element element)
        {
            bufferMutex.WaitOne();
            buffer.Add(element);
            bufferSize++;
            bufferMutex.ReleaseMutex();

        }
        /// <summary>
        /// Enqueue a new unique element
        /// </summary>
        /// <param name="element">Element to add</param>
        public void AddUnique(Element element)
        {
            bufferMutex.WaitOne();
            if (buffer.Contains(element))
            {
                bufferMutex.ReleaseMutex();
                return;
            }
            buffer.Add(element);
            bufferSize++;
            bufferMutex.ReleaseMutex();
        }
        #endregion
        #region Clear
        /// <summary>
        /// Clear de buffer
        /// </summary>
        public void Clear()
        {
            bufferMutex.WaitOne();
            bufferSize = 0;
            buffer.Clear();
            bufferMutex.ReleaseMutex();
        }
        #endregion
        #region Override   
        public override string ToString()
        {
            string result = "";
            foreach (Element e in buffer)
            {
                result += e.ToString() + " ";
            }
            return result;
        }

        public void Dispose()
        {
            bufferMutex.Dispose();
        }
        #endregion

    }
    public class MuseBuffer<Element> : IDisposable
    {
        //Storage
        private readonly Queue<Element> buffer;
        //Mutex
        private readonly Mutex bufferMutex;
        //Properties
        private int bufferSize;

        #region Constructor
        public MuseBuffer()
        {
            buffer = new Queue<Element>();
            bufferMutex = new Mutex(false);
            bufferSize = 0;
        }
        #endregion
        #region Getters and Setters
        public int getSize() { bufferMutex.WaitOne(); int size = bufferSize; bufferMutex.ReleaseMutex(); return size; }
        #endregion
        #region Dequeue
        /// <summary>
        /// Dequeues the oldest element of the buffer
        /// </summary>
        /// <returns>Oldest element of the buffer</returns>
        public Element Dequeue()
        {
            bufferMutex.WaitOne();
            if (bufferSize > 0)
            {
                Element result = buffer.Dequeue();
                bufferSize--;
                bufferMutex.ReleaseMutex();

                return result;

            }
            bufferMutex.ReleaseMutex();
            return default(Element);
        }
        /// <summary>
        /// Gets the first element of the buffer
        /// </summary>
        /// <returns>First element of the buffer</returns>
        public Element GetFirst()
        {
            bufferMutex.WaitOne();
            if (bufferSize > 0)
            {
                Element result = buffer.First();
                bufferMutex.ReleaseMutex();

                return result;

            }
            bufferMutex.ReleaseMutex();
            return default(Element);
        }
        /// <summary>
        /// Dequeues the target times
        /// </summary>
        /// <param name="size">Times to dequeue</param>
        public void Remove(uint size)
        {
            bufferMutex.WaitOne();
            for (uint i = 0; i < size; i++)
            {
                buffer.Dequeue();
                bufferSize--;
            }
            bufferMutex.ReleaseMutex();
        }
        #endregion
        #region Enqueue
        /// <summary>
        /// Enqueue a new element
        /// </summary>
        /// <param name="element">Element to add</param>
        public void Add(Element element)
        {
            bufferMutex.WaitOne();
            buffer.Enqueue(element);
            bufferSize++;
            bufferMutex.ReleaseMutex();

        }
        /// <summary>
        /// Enqueue a new unique element
        /// </summary>
        /// <param name="element">Element to add</param>
        public void AddUnique(Element element)
        {
            bufferMutex.WaitOne();
            if (buffer.Contains(element))
            {
                bufferMutex.ReleaseMutex();
                return;
            }
            buffer.Enqueue(element);
            bufferSize++;
            bufferMutex.ReleaseMutex();
        }
        #endregion
        #region Clear
        /// <summary>
        /// Clear de buffer
        /// </summary>
        public void Clear()
        {
            bufferMutex.WaitOne();
            bufferSize = 0;
            buffer.Clear();
            bufferMutex.ReleaseMutex();
        }
        public void Dispose()
        {
            bufferMutex.Dispose();
        }
        #endregion

    }
}

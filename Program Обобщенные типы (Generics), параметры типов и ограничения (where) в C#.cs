using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Program19
{
    namespace GenericTelemetryVault
    {
        // =========================================================
        // Шаг 1. Обобщенная структура ExecutionResult<T>
        // =========================================================
        public readonly struct ExecutionResult<T>
        {
            public bool Success { get; }
            public T Payload { get; }
            public string Message { get; }

            // Конструктор успеха
            public ExecutionResult(T payload)
            {
                Success = true;
                Payload = payload;
                Message = "OK";
            }

            // Конструктор ошибки: Payload получает default(T)
            public ExecutionResult(string error)
            {
                Success = false;
                Payload = default(T);
                Message = error;
            }

            // Статические фабричные методы
            public static ExecutionResult<T> Ok(T data) => new ExecutionResult<T>(data);
            public static ExecutionResult<T> Fail(string error) => new ExecutionResult<T>(error);
        }

        // =========================================================
        // Шаг 2. Интерфейс сущности с идентификатором
        // =========================================================
        public interface IIdentifiable<TKey>
        {
            TKey Id { get; }
        }

        // Телеметрический пакет: IIdentifiable<string> + IComparable
        public class SensorDataPayload : IIdentifiable<string>, IComparable<SensorDataPayload>
        {
            public string Id { get; }
            public double Value { get; }
            public DateTime Timestamp { get; }

            public SensorDataPayload(string id, double value)
            {
                Id = id;
                Value = value;
                Timestamp = DateTime.Now;
            }

            // Сравнение по полю Value
            public int CompareTo(SensorDataPayload other)
            {
                if (other == null) return 1;
                return Value.CompareTo(other.Value);
            }

            public override string ToString()
            {
                return $"[Пакет {Id}]: {Value:F2} ({Timestamp:T})";
            }
        }

        // =========================================================
        // Шаг 3. Обобщенный кольцевой буфер CircularBuffer<T>
        // =========================================================
        public class CircularBuffer<T> where T : class
        {
            private readonly T[] _buffer; // внутренний фиксированный массив
            private int _head;            // указатель на текущую позицию записи
            private int _count;           // фактическое число элементов

            public CircularBuffer(int capacity)
            {
                if (capacity <= 0)
                    throw new ArgumentException("Емкость буфера должна быть положительной.", nameof(capacity));

                _buffer = new T[capacity];
                _head = 0;
                _count = 0;
            }

            public int Capacity => _buffer.Length;
            public int Count => _count;
            public bool IsFull => _count == _buffer.Length;

            // Запись элемента: при переполнении перезаписывает старейший
            public void Push(T item)
            {
                _buffer[_head] = item;
                _head = (_head + 1) % _buffer.Length; // кольцевое смещение

                if (_count < _buffer.Length)
                {
                    _count++;
                }
            }

            // Чтение последнего добавленного элемента через ExecutionResult<T>
            public ExecutionResult<T> ReadLatest()
            {
                if (_count == 0)
                {
                    return ExecutionResult<T>.Fail("Буфер пуст!");
                }

                // _head указывает на следующую СВОБОДНУЮ ячейку (или на ячейку,
                // которая была перезаписана последней) — значит, последний
                // добавленный элемент находится на одну позицию позади _head
                int lastIndex = (_head - 1 + _buffer.Length) % _buffer.Length;
                return ExecutionResult<T>.Ok(_buffer[lastIndex]);
            }

            // Новый массив точной длины с элементами в порядке добавления
            public T[] ToArray()
            {
                T[] result = new T[_count];

                for (int i = 0; i < _count; i++)
                {
                    // Вычисляем реальный индекс в кольцевом массиве:
                    // самый старый элемент лежит в _head, когда буфер полон
                    int bufferIndex;
                    if (_count == _buffer.Length)
                    {
                        bufferIndex = (_head + i) % _buffer.Length;
                    }
                    else
                    {
                        bufferIndex = i; // буфер не полон — запись шла с нуля
                    }

                    result[i] = _buffer[bufferIndex];
                }

                return result;
            }
        }

        // =========================================================
        // Шаг 4. Обобщенные утилитные алгоритмы
        // =========================================================
        public static class AlgorithmUtility
        {
            // Поиск максимума: ограничение where T : IComparable<T>
            public static T FindPeak<T>(T[] items) where T : IComparable<T>
            {
                if (items == null || items.Length == 0)
                {
                    return default; // значение по умолчанию для пустого массива
                }

                T max = items[0];
                for (int i = 1; i < items.Length; i++)
                {
                    if (items[i].CompareTo(max) > 0)
                    {
                        max = items[i];
                    }
                }

                return max;
            }

            // Фильтрация предикатом: вручную, без LINQ
            public static T[] FilterByCondition<T>(T[] source, Predicate<T> match)
            {
                // Первый проход: считаем количество подходящих элементов
                int matchCount = 0;
                foreach (T item in source)
                {
                    if (match(item))
                    {
                        matchCount++;
                    }
                }

                // Второй проход: копируем в массив точного размера
                T[] result = new T[matchCount];
                int index = 0;
                foreach (T item in source)
                {
                    if (match(item))
                    {
                        result[index] = item;
                        index++;
                    }
                }

                return result;
            }

            // Печать коллекции с заголовком
            public static void PrintCollection<T>(string header, T[] items)
            {
                Console.WriteLine($"\n>>> {header}");
                foreach (T item in items)
                {
                    Console.WriteLine("  " + item);
                }

                if (items.Length == 0)
                {
                    Console.WriteLine("  (коллекция пуста)");
                }
            }
        }

        // =========================================================
        // Шаг 5. Демонстрационный сценарий
        // =========================================================
        class Program
        {
            static void Main()
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8;
                Console.WriteLine("=== УНИВЕРСАЛЬНОЕ ПАРАМЕТРИЧЕСКОЕ ХРАНИЛИЩЕ ТЕЛЕМЕТРИИ ===\n");

                // ---------------------------------------------------------
                // 5.1. Тестирование ExecutionResult<T> с разными типами
                // ---------------------------------------------------------
                Console.WriteLine("--- ЧАСТЬ 1: ExecutionResult<T> ---\n");

                // Успешный результат с типом int
                ExecutionResult<int> okResult = ExecutionResult<int>.Ok(42);
                Console.WriteLine($"Успех: Success={okResult.Success}, Payload={okResult.Payload}, Message=\"{okResult.Message}\"");

                // Ошибка: Payload должен получить default(int), то есть 0
                ExecutionResult<int> failResult = ExecutionResult<int>.Fail("Сбой датчика");
                Console.WriteLine($"Ошибка: Success={failResult.Success}, Payload={failResult.Payload} (default для int), Message=\"{failResult.Message}\"");

                // Демонстрация default для ссылочного типа
                ExecutionResult<SensorDataPayload> failRef = ExecutionResult<SensorDataPayload>.Fail("Нет связи");
                Console.WriteLine($"Ошибка (ссылочный тип): Success={failRef.Success}, Payload={(failRef.Payload == null ? "null (default для класса)" : failRef.Payload.ToString())}\n");

                // ---------------------------------------------------------
                // 5.2. Работа с обобщенным буфером CircularBuffer<T>
                // ---------------------------------------------------------
                Console.WriteLine("--- ЧАСТЬ 2: кольцевой буфер CircularBuffer<T> ---\n");

                var buffer = new CircularBuffer<SensorDataPayload>(3);
                Console.WriteLine($"Создан буфер: Capacity={buffer.Capacity}, Count={buffer.Count}, IsFull={buffer.IsFull}");

                // Чтение из пустого буфера — проверка Fail
                ExecutionResult<SensorDataPayload> emptyRead = buffer.ReadLatest();
                Console.WriteLine($"Чтение из пустого буфера: Success={emptyRead.Success}, Message=\"{emptyRead.Message}\"\n");

                // Добавляем 4 пакета (емкость 3 — старейший P-01 будет перезаписан)
                buffer.Push(new SensorDataPayload("P-01", 12.5));
                buffer.Push(new SensorDataPayload("P-02", 88.0));
                buffer.Push(new SensorDataPayload("P-03", 45.3));

                Console.WriteLine($"После 3 записей: Count={buffer.Count}, IsFull={buffer.IsFull}");

                buffer.Push(new SensorDataPayload("P-04", 99.1)); // перезаписывает P-01

                Console.WriteLine($"После 4-й записи: Count={buffer.Count} (не вырос — старейший P-01 перезаписан!)");
                Console.WriteLine($"Буфер содержит P-01? Проверим через ToArray ниже.\n");

                // Чтение последнего добавленного элемента
                ExecutionResult<SensorDataPayload> latest = buffer.ReadLatest();
                Console.WriteLine($"ReadLatest: Success={latest.Success}, Message=\"{latest.Message}\"");
                Console.WriteLine($"Последний пакет: {latest.Payload}\n");

                // ---------------------------------------------------------
                // 5.3. Демонстрация обобщенных алгоритмов
                // ---------------------------------------------------------
                Console.WriteLine("--- ЧАСТЬ 3: обобщенные алгоритмы ---\n");

                // Преобразование буфера в массив
                SensorDataPayload[] packets = buffer.ToArray();
                Console.WriteLine("Содержимое буфера (в порядке добавления, P-01 вытеснен):");
                AlgorithmUtility.PrintCollection("Пакеты из буфера:", packets);

                // Поиск максимума среди пакетов
                SensorDataPayload peak = AlgorithmUtility.FindPeak(packets);
                Console.WriteLine($"\nFindPeak (пакеты): {peak} — ожидается P-04 со значением 99.10");

                // Универсальность шаблона: тот же метод на массиве чисел
                int[] numbers = { 15, 89, 4, 102, 63 };
                int maxNumber = AlgorithmUtility.FindPeak(numbers);
                Console.WriteLine($"FindPeak (числа int): {maxNumber} — ожидается 102");

                // Фильтрация пакетов со значением > 50
                SensorDataPayload[] filtered = AlgorithmUtility.FilterByCondition(packets, p => p.Value > 50.0);
                AlgorithmUtility.PrintCollection("Пакеты со значением > 50 (Predicate-лямбда):", filtered);

                // Дополнительная демонстрация: фильтр чисел
                int[] bigNumbers = AlgorithmUtility.FilterByCondition(numbers, n => n >= 60);
                AlgorithmUtility.PrintCollection("Числа >= 60 (та же обобщенная логика):", bigNumbers);

                Console.WriteLine("\nНажмите любую клавишу для завершения...");
                Console.ReadKey();
            }
        }
    }
}


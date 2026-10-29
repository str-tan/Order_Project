using Moq;
using Order_Project.Models;
using Order_Project.Services;
using Order_Project.Services.Intefraces;

namespace Order_Project_Tests
{
    public class OrderServiceTests
    {
        private readonly Mock<IInventoryService> _inventoryMock;
        private readonly Mock<IPaymentService> _paymentMock;
        private readonly Mock<INotificationService> _notificationMock;
        private readonly OrderService _service;

        public OrderServiceTests()
        {
            _inventoryMock = new Mock<IInventoryService>();
            _paymentMock = new Mock<IPaymentService>();
            _notificationMock = new Mock<INotificationService>();

            _service = new OrderService(_inventoryMock.Object, _paymentMock.Object, _notificationMock.Object);
        }
        /// <summary>
        /// Тест 1. Перевіряє, що при передачі порожнього імені продукту викидається ArgumentException.
        /// Використані перевірки: Assert.Throws.
        /// </summary>
        [Fact]
        public void CreateOrder_EmptyProduct_ThrowsArgumentException()
        {
            // Arrange
            string product = "";
            int quantity = 5;

            // Act
            Action act = () => _service.CreateOrder(product, quantity);

            // Assert
            Assert.Throws<ArgumentException>(act);
        }

        /// <summary>
        /// Тест 2. Перевіряє, що при невалідній кількості товару викидається виняток. 
        /// Використані перевірки: [Theory], [InlineData], Assert.Throws.
        /// </summary>
        [Theory]
        [InlineData("Laptop", 0)]
        [InlineData("Mouse", -1)]
        [InlineData("Keyboard", -10)]
        public void CreateOrder_InvalidQuantity_ThrowsArgumentException(string product, int quantity)
        {
            // Arrange (не вимагається додаткових налаштувань для моків)

            // Act
            Action act = () => _service.CreateOrder(product, quantity);

            // Assert
            Assert.Throws<ArgumentException>(act);
        }

        /// <summary>
        /// Тест 3. Перевіряє, що якщо на складі немає товару, метод оплати ніколи не викликається.
        /// Використані перевірки: Assert.Throws, It.IsAny(), Verify(..., Times.Never).
        /// </summary>
        [Fact]
        public void CreateOrder_NotEnoughStock_ThrowsExceptionAndNeverCallsPayment()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(false);

            // Act
            Action act = () => _service.CreateOrder("Monitor", 2);

            // Assert
            Assert.Throws<InvalidOperationException>(act);
            _paymentMock.Verify(p => p.ProcessPayment(It.IsAny<Order>()), Times.Never);
        }

        /// <summary>
        /// Тест 4. Перевіряє успішне створення замовлення.
        /// Використані перевірки: Assert.NotNull, Assert.Equal, Verify().
        /// </summary>
        [Fact]
        public void CreateOrder_ValidInput_ReturnsOrderAndSendsNotification()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock("Laptop", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            // Act
            var result = _service.CreateOrder("Laptop", 1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Laptop", result.Product);
            _notificationMock.Verify(n => n.SendConfirmation(result));
        }

        /// <summary>
        /// Тест 5. Перевіряє скасування замовлення при провалі оплати. Товар має повернутись на склад рівно 1 раз.
        /// Використані перевірки: Assert.Throws, It.Is(predicate), Verify(..., Times.Exactly(n)).
        /// </summary>
        [Fact]
        public void CreateOrder_PaymentFailed_RevertsStockAndThrows()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock("Phone", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(false);

            // Act
            Action act = () => _service.CreateOrder("Phone", 1);

            // Assert
            Assert.Throws<InvalidOperationException>(act);
            _inventoryMock.Verify(i => i.IncreaseStock(It.Is<string>(s => s == "Phone"), 1), Times.Exactly(1));
        }

        /// <summary>
        /// Тест 6. Перевіряє, що новостворений сервіс має порожній список замовлень.
        /// Використані перевірки: Assert.Empty.
        /// </summary>
        [Fact]
        public void GetOrders_Initially_ReturnsEmptyCollection()
        {
            // Arrange (список створюється пустим у конструкторі сервісу)

            // Act
            var orders = _service.GetOrders();

            // Assert
            Assert.Empty(orders);
        }

        /// <summary>
        /// Тест 7. Перевіряє, що після успішного замовлення воно з'являється у списку.
        /// Використані перевірки: Assert.NotEmpty, Assert.Contains.
        /// </summary>
        [Fact]
        public void GetOrders_AfterCreation_ReturnsNotEmptyListContainingOrder()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock("Monitor", 2)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var newOrder = _service.CreateOrder("Monitor", 2);

            // Act
            var orders = _service.GetOrders();

            // Assert
            Assert.NotEmpty(orders);
            Assert.Contains(newOrder, orders);
        }

        /// <summary>
        /// Тест 8. Перевіряє оновлення існуючого замовлення.
        /// Використані перевірки: Assert.True, Assert.NotEqual, Assert.Equal.
        /// </summary>
        [Fact]
        public void UpdateOrder_ValidOrderAndQuantity_UpdatesSuccessfully()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock("Desk", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("Desk", 1);
            int oldQuantity = order.Quantity;
            int newQuantity = 5;

            // Act
            bool updateResult = _service.UpdateOrder(order.Id, newQuantity);

            // Assert
            Assert.True(updateResult);
            Assert.NotEqual(oldQuantity, order.Quantity);
            Assert.Equal(newQuantity, order.Quantity);
        }

        /// <summary>
        /// Тест 9. Перевіряє спробу оновити замовлення з неіснуючим ID.
        /// Використані перевірки: Assert.False.
        /// </summary>
        [Fact]
        public void UpdateOrder_NonExistingId_ReturnsFalse()
        {
            // Arrange
            int nonExistingId = 999;

            // Act
            bool result = _service.UpdateOrder(nonExistingId, 2);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Тест 10. Перевіряє, що при видаленні замовлення товар повертається на склад.
        /// Використані перевірки: Assert.True, Verify(..., Times.AtLeastOnce).
        /// </summary>
        [Fact]
        public void RemoveOrder_ExistingOrder_RemovesAndIncreasesStock()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock("Tablet", 3)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("Tablet", 3);

            // Act
            bool result = _service.RemoveOrder(order.Id);

            // Assert
            Assert.True(result);
            _inventoryMock.Verify(i => i.IncreaseStock("Tablet", 3), Times.AtLeastOnce);
        }

        /// <summary>
        /// Тест 11. Перевіряє спробу видалити замовлення з неіснуючим ID.
        /// Використані перевірки: Assert.False.
        /// </summary>
        [Fact]
        public void RemoveOrder_NonExistingOrder_ReturnsFalse()
        {
            // Arrange
            int nonExistingId = 1234;

            // Act
            bool result = _service.RemoveOrder(nonExistingId);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Тест 12. Перевіряє, що під час обробки замовлення викликається зменшення залишку на складі з будь-якими параметрами.
        /// Використані перевірки: It.IsAny(), Verify().
        /// </summary>
        [Fact]
        public void CreateOrder_AlwaysCallsReduceStock()
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock(It.IsAny<string>(), It.IsAny<int>())).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);

            // Act
            _service.CreateOrder("Cable", 10);

            // Assert
            _inventoryMock.Verify(i => i.ReduceStock(It.IsAny<string>(), It.IsAny<int>()));
        }

        /// <summary>
        /// Тест 13. Додатковий сценарій. Перевіряє спробу оновити замовлення невалідною кількістю.
        /// Використані перевірки: [Theory], [InlineData], Assert.False.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void UpdateOrder_InvalidQuantity_ReturnsFalse(int invalidQuantity)
        {
            // Arrange
            _inventoryMock.Setup(i => i.CheckStock("Mousepad", 1)).Returns(true);
            _paymentMock.Setup(p => p.ProcessPayment(It.IsAny<Order>())).Returns(true);
            var order = _service.CreateOrder("Mousepad", 1);

            // Act
            bool result = _service.UpdateOrder(order.Id, invalidQuantity);

            // Assert
            Assert.False(result);
        }

    }

}

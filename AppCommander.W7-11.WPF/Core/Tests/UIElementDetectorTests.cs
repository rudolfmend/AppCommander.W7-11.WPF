using System;
using System.Windows.Automation;
using Moq;
using Xunit;
using AppCommander.W7_11.WPF.Core;

namespace AppCommander.W7_11.WPF.Tests.Core
{
    /// <summary>
    /// Unit testy pre UIElementDetector.GetElementProperty metódu
    /// </summary>
    public class UIElementDetectorTests
    {
        #region GetElementProperty Tests

        [Fact]
        public void GetElementProperty_ValidNameProperty_ReturnsCorrectValue()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();
            var expectedName = "Submit Button";

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Returns(expectedName);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal(expectedName, result);
        }

        [Fact]
        public void GetElementProperty_ValidAutomationIdProperty_ReturnsCorrectValue()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();
            var expectedId = "btnSubmit";

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.AutomationIdProperty))
                .Returns(expectedId);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.AutomationIdProperty);

            // Assert
            Assert.Equal(expectedId, result);
        }

        [Fact]
        public void GetElementProperty_ValidClassNameProperty_ReturnsCorrectValue()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();
            var expectedClassName = "Button";

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.ClassNameProperty))
                .Returns(expectedClassName);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.ClassNameProperty);

            // Assert
            Assert.Equal(expectedClassName, result);
        }

        [Fact]
        public void GetElementProperty_NullValue_ReturnsEmptyString()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.HelpTextProperty))
                .Returns((object)null);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.HelpTextProperty);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetElementProperty_EmptyString_ReturnsEmptyString()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Returns("");

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetElementProperty_ElementNotAvailableException_ReturnsEmptyString()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Throws(new ElementNotAvailableException("Element nie je dostupný - typicky pri písaní do sledovaného UI elementu"));

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetElementProperty_InvalidOperationException_ReturnsEmptyString()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Throws(new InvalidOperationException("Neplatná operácia"));

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetElementProperty_UnauthorizedAccessException_ReturnsEmptyString()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Throws(new UnauthorizedAccessException("Prístup zamietnutý"));

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetElementProperty_COMException_ReturnsEmptyString()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Throws(new System.Runtime.InteropServices.COMException("COM error"));

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetElementProperty_IntegerValue_ReturnsStringRepresentation()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();
            var processId = 12345;

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty))
                .Returns(processId);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.ProcessIdProperty);

            // Assert
            Assert.Equal("12345", result);
        }

        [Fact]
        public void GetElementProperty_BooleanValue_ReturnsStringRepresentation()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty))
                .Returns(true);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.IsEnabledProperty);

            // Assert
            Assert.Equal("True", result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("Prihlásenie")]
        [InlineData("Microsoft.UI.Content.DesktopChildSiteBridge")]
        [InlineData("Button_Submit_123")]
        [InlineData("text@email.com")]
        public void GetElementProperty_VariousStringValues_ReturnsCorrectString(string value)
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Returns(value);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public void GetElementProperty_HelpTextProperty_ReturnsCorrectValue()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();
            var expectedHelpText = "Zadajte svoje meno";

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.HelpTextProperty))
                .Returns(expectedHelpText);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.HelpTextProperty);

            // Assert
            Assert.Equal(expectedHelpText, result);
        }

        [Fact]
        public void GetElementProperty_AccessKeyProperty_ReturnsCorrectValue()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();
            var expectedAccessKey = "Alt+S";

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.AccessKeyProperty))
                .Returns(expectedAccessKey);

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.AccessKeyProperty);

            // Assert
            Assert.Equal(expectedAccessKey, result);
        }

        [Fact]
        public void GetElementProperty_WinUI3Element_HandlesException()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();

            // Simuluj situáciu s WinUI3 elementom, kde nastáva problém pri získavaní vlastnosti
            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Throws(new ElementNotAvailableException("WinUI3 element not available"));

            // Act
            var result = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void GetElementProperty_MultipleCallsSameElement_ReturnsConsistentResults()
        {
            // Arrange
            var mockElement = new Mock<AutomationElement>();
            var expectedValue = "Consistent Value";

            mockElement
                .Setup(x => x.GetCurrentPropertyValue(AutomationElement.NameProperty))
                .Returns(expectedValue);

            // Act
            var result1 = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);
            var result2 = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);
            var result3 = InvokePrivateGetElementProperty(mockElement.Object, AutomationElement.NameProperty);

            // Assert
            Assert.Equal(expectedValue, result1);
            Assert.Equal(expectedValue, result2);
            Assert.Equal(expectedValue, result3);
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper metóda na volanie private GetElementProperty metódy cez reflection
        /// </summary>
        private string InvokePrivateGetElementProperty(AutomationElement element, AutomationProperty property)
        {
            var method = typeof(UIElementDetector).GetMethod(
                "GetElementProperty",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
            );

            if (method == null)
                throw new Exception("GetElementProperty metóda nebola nájdená");

            return (string)method.Invoke(null, new object[] { element, property });
        }

        #endregion
    }
}

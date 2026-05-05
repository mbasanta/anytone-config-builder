using AnytoneConfigBuilder.Core.Validation;

namespace AnytoneConfigBuilder.Core.Tests;

public sealed class EdgeCaseValidationTests
{
    #region Channel Name Length Tests
    
    [Fact]
    public void ValidateName_WithinMaxLength_ReturnsName()
    {
        var result = InputValidator.ValidateName("ValidName");
        Assert.Equal("ValidName", result);
    }

    [Fact]
    public void ValidateName_AtMaxLength_ReturnsName()
    {
        var maxLengthName = new string('A', 16);
        var result = InputValidator.ValidateName(maxLengthName);
        Assert.Equal(maxLengthName, result);
    }

    [Fact]
    public void ValidateName_ExceedsMaxLength_ThrowsException()
    {
        var tooLongName = new string('A', 17);
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateName(tooLongName));
        Assert.Contains("exceeds max length of 16", exception.Message);
    }

    [Fact]
    public void ValidateName_ExceedsMaxLengthBy10_ThrowsException()
    {
        var tooLongName = new string('Z', 26);
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateName(tooLongName));
        Assert.Contains("Channel Name", exception.Message);
    }

    #endregion

    #region Zone Name Length Tests

    [Fact]
    public void ValidateZone_WithinMaxLength_ReturnsZone()
    {
        var result = InputValidator.ValidateZone("Zone1");
        Assert.Equal("Zone1", result);
    }

    [Fact]
    public void ValidateZone_AtMaxLength_ReturnsZone()
    {
        var maxLengthZone = new string('Z', 16);
        var result = InputValidator.ValidateZone(maxLengthZone);
        Assert.Equal(maxLengthZone, result);
    }

    [Fact]
    public void ValidateZone_ExceedsMaxLength_ThrowsException()
    {
        var tooLongZone = new string('Z', 17);
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateZone(tooLongZone));
        Assert.Contains("exceeds max length of 16", exception.Message);
    }

    #endregion

    #region Frequency Boundary Tests

    [Fact]
    public void ValidateFreq_MinBoundary_ReturnsValue()
    {
        var result = InputValidator.ValidateFreq("0");
        Assert.Equal("0", result);
    }

    [Fact]
    public void ValidateFreq_MaxBoundary_ReturnsValue()
    {
        var result = InputValidator.ValidateFreq("500");
        Assert.Equal("500", result);
    }

    [Fact]
    public void ValidateFreq_MidRange_ReturnsValue()
    {
        var result = InputValidator.ValidateFreq("145.5");
        Assert.Equal("145.5", result);
    }

    [Fact]
    public void ValidateFreq_BelowMinimum_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateFreq("-1"));
        Assert.Contains("Frequency", exception.Message);
        Assert.Contains("range", exception.Message);
    }

    [Fact]
    public void ValidateFreq_AboveMaximum_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateFreq("501"));
        Assert.Contains("Frequency", exception.Message);
        Assert.Contains("range", exception.Message);
    }

    [Fact]
    public void ValidateFreq_NonNumeric_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateFreq("ABC"));
        Assert.Contains("Frequency", exception.Message);
    }

    [Fact]
    public void ValidateFreq_Empty_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateFreq(""));
        Assert.Contains("Frequency", exception.Message);
    }

    #endregion

    #region CTCSS/DCS Tone Validation Tests

    [Fact]
    public void ValidateCtcss_OffValue_ReturnsOff()
    {
        var result = InputValidator.ValidateCtcss("Off");
        Assert.Equal("Off", result);
    }

    [Fact]
    public void ValidateCtcss_OffLowercase_ReturnsOff()
    {
        var result = InputValidator.ValidateCtcss("off");
        Assert.Equal("Off", result);
    }

    [Fact]
    public void ValidateCtcss_DcsCodeLetter_ReturnsCode()
    {
        var result = InputValidator.ValidateCtcss("D25N");
        Assert.Equal("D25N", result);
    }

    [Fact]
    public void ValidateCtcss_DcsCodeLowercase_ReturnsCode()
    {
        var result = InputValidator.ValidateCtcss("d123i");
        Assert.Equal("d123i", result);
    }

    [Fact]
    public void ValidateCtcss_NumericMinValue_ReturnsValue()
    {
        var result = InputValidator.ValidateCtcss("0");
        Assert.Equal("0", result);
    }

    [Fact]
    public void ValidateCtcss_NumericMaxValue_ReturnsValue()
    {
        var result = InputValidator.ValidateCtcss("300");
        Assert.Equal("300", result);
    }

    [Fact]
    public void ValidateCtcss_NumericMidValue_ReturnsValue()
    {
        var result = InputValidator.ValidateCtcss("88.5");
        Assert.Equal("88.5", result);
    }

    [Fact]
    public void ValidateCtcss_NumericBelowRange_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateCtcss("-1"));
        Assert.Contains("CTCSS/DCS", exception.Message);
    }

    [Fact]
    public void ValidateCtcss_NumericAboveRange_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateCtcss("301"));
        Assert.Contains("CTCSS/DCS", exception.Message);
    }

    [Fact]
    public void ValidateCtcss_DcsCodeSingleLetter_ReturnsCode()
    {
        var result = InputValidator.ValidateCtcss("D");
        Assert.Equal("D", result);
    }

    [Fact]
    public void ValidateCtcss_DcsCodeTooLong_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateCtcss("D12345678901"));
        Assert.Contains("CTCSS/DCS", exception.Message);
    }

    #endregion

    #region Call Type Validation Tests

    [Fact]
    public void ValidateCallType_GroupCallValue_ReturnsGroupCall()
    {
        var result = InputValidator.ValidateCallType("Group Call");
        Assert.Equal("Group Call", result);
    }

    [Fact]
    public void ValidateCallType_PrivateCallValue_ReturnsPrivateCall()
    {
        var result = InputValidator.ValidateCallType("Private Call");
        Assert.Equal("Private Call", result);
    }

    [Fact]
    public void ValidateCallType_InvalidValue_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateCallType("Mixed"));
        Assert.Contains("Call Type", exception.Message);
    }

    [Fact]
    public void ValidateCallType_CaseInsensitive_ReturnsCorrectCasing()
    {
        var result = InputValidator.ValidateCallType("private call");
        Assert.Equal("Private Call", result);
    }

    #endregion

    #region Color Code Validation Tests

    [Fact]
    public void ValidateColorCode_MinValue_ReturnsValue()
    {
        var result = InputValidator.ValidateColorCode("0");
        Assert.Equal("0", result);
    }

    [Fact]
    public void ValidateColorCode_MaxValue_ReturnsValue()
    {
        var result = InputValidator.ValidateColorCode("16");
        Assert.Equal("16", result);
    }

    [Fact]
    public void ValidateColorCode_MidValue_ReturnsValue()
    {
        var result = InputValidator.ValidateColorCode("8");
        Assert.Equal("8", result);
    }

    [Fact]
    public void ValidateColorCode_BelowMin_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateColorCode("-1"));
        Assert.Contains("Color Code", exception.Message);
    }

    [Fact]
    public void ValidateColorCode_AboveMax_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateColorCode("17"));
        Assert.Contains("Color Code", exception.Message);
    }

    [Fact]
    public void ValidateColorCode_NonNumeric_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateColorCode("ABC"));
        Assert.Contains("Color Code", exception.Message);
    }

    #endregion

    #region Contact Name Length Tests

    [Fact]
    public void ValidateContact_WithinMaxLength_ReturnsContact()
    {
        var result = InputValidator.ValidateContact("MyContact");
        Assert.Equal("MyContact", result);
    }

    [Fact]
    public void ValidateContact_AtMaxLength_ReturnsContact()
    {
        var maxLengthContact = new string('C', 16);
        var result = InputValidator.ValidateContact(maxLengthContact);
        Assert.Equal(maxLengthContact, result);
    }

    [Fact]
    public void ValidateContact_ExceedsMaxLength_ThrowsException()
    {
        var tooLongContact = new string('C', 17);
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateContact(tooLongContact));
        Assert.Contains("exceeds max length of 16", exception.Message);
    }

    #endregion

    #region Timeslot Validation Tests

    [Fact]
    public void ValidateTimeslot_Slot1_ReturnsSlot1()
    {
        var result = InputValidator.ValidateTimeslot("1");
        Assert.Equal("1", result);
    }

    [Fact]
    public void ValidateTimeslot_Slot2_ReturnsSlot2()
    {
        var result = InputValidator.ValidateTimeslot("2");
        Assert.Equal("2", result);
    }

    [Fact]
    public void ValidateTimeslot_Dash_ReturnsDash()
    {
        var result = InputValidator.ValidateTimeslot("-");
        Assert.Equal("-", result);
    }

    [Fact]
    public void ValidateTimeslot_Invalid_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateTimeslot("3"));
        Assert.Contains("Time Slot", exception.Message);
    }

    #endregion

    #region Bandwidth Validation Tests

    [Fact]
    public void ValidateBandwidth_25K_ReturnsValue()
    {
        var result = InputValidator.ValidateBandwidth("25K");
        Assert.Equal("25K", result);
    }

    [Fact]
    public void ValidateBandwidth_12Point5K_ReturnsValue()
    {
        var result = InputValidator.ValidateBandwidth("12.5K");
        Assert.Equal("12.5K", result);
    }

    [Fact]
    public void ValidateBandwidth_Invalid_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateBandwidth("20K"));
        Assert.Contains("Bandwidth", exception.Message);
    }

    #endregion

    #region Power Level Validation Tests

    [Fact]
    public void ValidatePower_Low_ReturnsLow()
    {
        var result = InputValidator.ValidatePower("Low");
        Assert.Equal("Low", result);
    }

    [Fact]
    public void ValidatePower_Mid_ReturnsMid()
    {
        var result = InputValidator.ValidatePower("Mid");
        Assert.Equal("Mid", result);
    }

    [Fact]
    public void ValidatePower_High_ReturnsHigh()
    {
        var result = InputValidator.ValidatePower("High");
        Assert.Equal("High", result);
    }

    [Fact]
    public void ValidatePower_Turbo_ReturnsTurbo()
    {
        var result = InputValidator.ValidatePower("Turbo");
        Assert.Equal("Turbo", result);
    }

    [Fact]
    public void ValidatePower_Invalid_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidatePower("Maximum"));
        Assert.Contains("Power", exception.Message);
    }

    #endregion

    #region TX Permit Validation Tests

    [Fact]
    public void ValidateTxPermit_Always_ReturnsAlways()
    {
        var result = InputValidator.ValidateTxPermit("Always");
        Assert.Equal("Always", result);
    }

    [Fact]
    public void ValidateTxPermit_ChannelFree_ReturnsChannelFree()
    {
        var result = InputValidator.ValidateTxPermit("ChannelFree");
        Assert.Equal("ChannelFree", result);
    }

    [Fact]
    public void ValidateTxPermit_SameColorCode_ReturnsSameColorCode()
    {
        var result = InputValidator.ValidateTxPermit("Same Color Code");
        Assert.Equal("Same Color Code", result);
    }

    [Fact]
    public void ValidateTxPermit_DifferentColorCode_ReturnsDifferentColorCode()
    {
        var result = InputValidator.ValidateTxPermit("Different Color Code");
        Assert.Equal("Different Color Code", result);
    }

    [Fact]
    public void ValidateTxPermit_Invalid_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateTxPermit("Custom"));
        Assert.Contains("TX Permit", exception.Message);
    }

    #endregion

    #region TX Prohibit Validation Tests

    [Fact]
    public void ValidateTxProhibit_On_ReturnsOn()
    {
        var result = InputValidator.ValidateTxProhibit("On");
        Assert.Equal("On", result);
    }

    [Fact]
    public void ValidateTxProhibit_Off_ReturnsOff()
    {
        var result = InputValidator.ValidateTxProhibit("Off");
        Assert.Equal("Off", result);
    }

    [Fact]
    public void ValidateTxProhibit_Invalid_ThrowsException()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => InputValidator.ValidateTxProhibit("Maybe"));
        Assert.Contains("TX Prohibit", exception.Message);
    }

    #endregion
}

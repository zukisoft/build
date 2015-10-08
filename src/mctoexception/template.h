//-----------------------------------------------------------------------------
// Copyright (c) 2015 Michael G. Brehm
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//-----------------------------------------------------------------------------

#ifndef __MESSAGEEXCEPTION_H_
#define __MESSAGEEXCEPTION_H_
#pragma once

#include <exception>
#include <vector>
#include <type_traits>
#include <Windows.h>

#pragma warning(push, 4)	

class MessageException : public std::exception
{
public:

	// messageid_t
	//
	// Message identifier data type
	using messageid_t = HRESULT;

	static_assert(sizeof(messageid_t) == sizeof(DWORD_PTR), 
		"Size of message identifier type must be 32 bits for compatibility with FormatMessage");

	// Instance Constructor
	//
	template <typename... _insertions>
	MessageException(messageid_t messageid, _insertions const&... insertions) : m_what(AllocateMessage(messageid, insertions...)), m_owned((m_what != nullptr)) 
	{
	}

	// Copy Constructor
	//
	MessageException(MessageException const& rhs) : m_what(rhs.m_what), m_owned(false) 
	{
	}

	// Destructor
	//
	virtual ~MessageException()
	{
		if(m_owned && (m_what != nullptr)) LocalFree(m_what);
	}

	// std::exception::what
	//
	// Gets a pointer to the exception message text
	virtual char const* what(void) const 
	{ 
		return m_what;
	}

	// s_module
	//
	// Initialized to the module handle of this compilation unit
	static HMODULE const s_module;

private:

	// is_charpointer<_type>
	//
	// Type traits used to determine if a template argument is of type char*
	template<typename _type> struct is_charpointer : public std::false_type {};
	template<> struct is_charpointer<char*> : public std::true_type {};
	template<> struct is_charpointer<char const*> : public std::true_type {};

	// is_wcharpointer<_type>
	//
	// Type traits used to determine if a template argument is of type wchar_t*
	template<typename _type> struct is_wcharpointer : public std::false_type {};
	template<> struct is_wcharpointer<wchar_t*> : public std::true_type {};
	template<> struct is_wcharpointer<wchar_t const*> : public std::true_type {};

	// AllocateMessage
	//
	// Generates the formatted exception message based on the message identifier and insertions
	template<typename... _remaining>
	static char* AllocateMessage(messageid_t messageid, _remaining const&... remaining)
	{
		std::vector<DWORD_PTR> arguments;
		return AllocateMessage(messageid, arguments, remaining...);
	}

	// AllocateMessage
	//
	// Intermediate variadic overload; converts a single insertion argument into a DWORD_PTR value
	template<typename _first, typename... _remaining>
	static auto AllocateMessage(messageid_t messageid, std::vector<DWORD_PTR>& arguments, _first const& first, _remaining const&... remaining)
		-> typename std::enable_if<!is_charpointer<_first>::value && !is_wcharpointer<_first>::value, char*>::type
	{
		static_assert(!std::is_floating_point<_first>::value, 
			"Floating point values cannot be specified as insertions to FormatMessage");
		
		static_assert(!(std::is_integral<_first>::value && sizeof(_first) > sizeof(DWORD_PTR)), 
			"Integral values larger than 32 bits in size cannot be specified as insertions to FormatMessage");

		arguments.push_back((DWORD_PTR)first);
		return AllocateMessage(messageid, arguments, remaining...);
	}

	// AllocateMessage
	//
	// Intermediate variadic overload; specialized for char* data types to handle null pointers
	template<typename _first, typename... _remaining>
	static auto AllocateMessage(messageid_t messageid, std::vector<DWORD_PTR>& arguments, _first const& first, _remaining const&... remaining) 
		-> typename std::enable_if<is_charpointer<_first>::value, char*>::type
	{
		arguments.push_back(reinterpret_cast<DWORD_PTR>(first == nullptr ? s_null : first));
		return AllocateMessage(messageid, arguments, remaining...);
	}

	// AllocateMessage
	//
	// Intermediate variadic overload; specialized for wchar_t* data type to handle null pointers
	template<typename _first, typename... _remaining>
	static auto AllocateMessage(messageid_t messageid, std::vector<DWORD_PTR>& arguments, _first const& first, _remaining const&... remaining) 
		-> typename std::enable_if<is_wcharpointer<_first>::value, char*>::type
	{
		arguments.push_back(reinterpret_cast<DWORD_PTR>(first == nullptr ? s_widenull : first));
		return AllocateMessage(messageid, arguments, remaining...);
	}

	// AllocateMessage
	//
	// Final variadic overload; generates the formatted message string with the collected insertions
	static char* AllocateMessage(messageid_t messageid, std::vector<DWORD_PTR>& arguments)
	{
		LPTSTR message = nullptr;					// Allocated string from ::FormatMessage

		// Attempt to format the message from the current module resources and provided insertions
		DWORD cchmessage = ::FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_HMODULE | FORMAT_MESSAGE_ARGUMENT_ARRAY, s_module, 
			static_cast<DWORD>(messageid), ::GetThreadUILanguage(), reinterpret_cast<LPTSTR>(&message), 0, reinterpret_cast<va_list*>(arguments.data())); 
		if(cchmessage == 0) {

			// The message could not be looked up in the specified module; generate the default message instead
			if(message) { LocalFree(message); message = nullptr; }
			cchmessage = ::FormatMessage(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_STRING | FORMAT_MESSAGE_ARGUMENT_ARRAY, 
				s_defaultformat, 0, 0, reinterpret_cast<LPTSTR>(&message), 0, reinterpret_cast<va_list*>(&messageid));
			if(cchmessage == 0) {

				// The default message could not be generated; give up
				if(message) ::LocalFree(message);
				return nullptr;
			}
		}

	#ifdef _UNICODE
		// UNICODE projects need to convert the message string into CP_UTF8 or CP_ACP
		int convertcch = ::WideCharToMultiByte(CP_UTF8, 0, message, cchmessage, nullptr, 0, nullptr, nullptr);
		char* converted = reinterpret_cast<char*>(::LocalAlloc(LMEM_FIXED | LMEM_ZEROINIT, (convertcch + 1) * sizeof(char)));
		if(converted) ::WideCharToMultiByte(CP_UTF8, 0, message, cchmessage, converted, convertcch, nullptr, nullptr);

		LocalFree(message);
		return converted;
	#else

		// ANSI/UTF-8 projects can use the string generated by ::FormatMessage directly
		return message;
	#endif
	}

	// Member Variables
	//
	char* const							m_what;	
	bool const							m_owned;
	static constexpr char const*		s_null			= "<null pointer>";
	static constexpr wchar_t const*		s_widenull		= L"<null pointer>";
	static constexpr LPCTSTR			s_defaultformat	= _T("Exception code 0x%1!08lX! : The message for this exception could not be generated.");
};

// MessageException::s_module
//
// Initialized to the module handle of this compilation unit
__declspec(selectany)
HMODULE const MessageException::s_module = []() -> HMODULE {

	HMODULE module = nullptr;

	::GetModuleHandleEx(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
		reinterpret_cast<LPCTSTR>(&MessageException::s_module), &module);

	return module;
}();

//-----------------------------------------------------------------------------

#pragma warning(pop)

#endif	// __EXCEPTION_H_

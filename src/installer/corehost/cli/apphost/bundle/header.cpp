// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "header.h"
#include "reader.h"
#include "error_codes.h"
#include "trace.h"

using namespace bundle;

bool header_fixed_t::is_valid() const
{
    return num_embedded_files > 0 &&
           ((major_version < header_t::major_version) ||
            (major_version == header_t::major_version && minor_version <= header_t::minor_version));
}

header_t header_t::read(reader_t& reader)
{
    const header_fixed_t* fixed_header = reinterpret_cast<const header_fixed_t*>(reader.read_direct(sizeof(header_fixed_t)));

    if (!fixed_header->is_valid())
    {
        trace::error(_X("Failure processing application bundle."));
        trace::error(_X("Bundle header version compatibility check failed."));

        throw StatusCode::BundleExtractionFailure;
    }

    header_t header(fixed_header->num_embedded_files);

    // bundle_id is a component of the extraction path
    reader.read_path_string(header.m_bundle_id);

    return header;
}
